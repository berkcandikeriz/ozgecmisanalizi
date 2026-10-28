using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.Core.Tools;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OzgecmisAnalizi.Business.Parsing;

public interface IResumeTextExtractor
{
    Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default);
}

/// <summary>PDF (PdfPig), DOCX (OpenXml) ve TXT dosyalarından düz metin çıkarır.</summary>
public sealed class ResumeTextExtractor : IResumeTextExtractor
{
    public async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var kind = FileGuard.DetectResumeKind(fileName, bytes.AsSpan(0, Math.Min(8, bytes.Length)), bytes.LongLength);

        var text = kind switch
        {
            "pdf" => ExtractPdf(bytes),
            "docx" => ExtractDocx(bytes),
            _ => DecodeText(bytes)
        };

        text = Normalize(text);
        if (text.Length < 80)
        {
            throw new BusinessException(
                "Dosyadan okunabilir metin çıkarılamadı. CV'niz taranmış bir görsel olabilir; lütfen metin seçilebilir bir PDF veya DOCX yükleyin.");
        }

        return text;
    }

    private static string ExtractPdf(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(bytes);
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(ContentOrderTextExtractor.GetText(page));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var line = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static string DecodeText(byte[] bytes)
    {
        // UTF-8 (BOM'lu/BOM'suz); başarısız olursa Windows-1254 Türkçe.
        var utf8 = new UTF8Encoding(false, true);
        try
        {
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254).GetString(bytes);
        }
    }

    private static string Normalize(string text)
    {
        var cleaned = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace(' ', ' ')
            .Replace("", "•")
            .Replace("●", "•")
            .Replace("▪", "•");

        var lines = cleaned.Split('\n').Select(l => l.TrimEnd());
        var sb = new StringBuilder();
        var blank = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (++blank > 1)
                {
                    continue;
                }
            }
            else
            {
                blank = 0;
            }

            sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }
}
