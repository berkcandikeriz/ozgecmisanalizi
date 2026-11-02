using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.Core.Tools;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OzgecmisAnalizi.Business.Parsing;

/// <summary>Dosyadan çıkarılan metin ve görünür metinde yer almayan köprü bağlantıları (LinkedIn ikonu, "Portfolyo" yazısı vb.).</summary>
public sealed record ExtractedDocument(string Text, IReadOnlyList<string> Links);

public interface IResumeTextExtractor
{
    Task<ExtractedDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default);
}

/// <summary>
/// PDF (PdfPig), DOCX (OpenXml) ve TXT dosyalarından düz metin çıkarır. CV'lerde LinkedIn/GitHub adresi çoğunlukla
/// tıklanabilir bir yazı veya ikonun arkasında durur; bu adresler metinde görünmediği için bağlantı katmanından ayrıca okunur.
/// </summary>
public sealed class ResumeTextExtractor : IResumeTextExtractor
{
    public async Task<ExtractedDocument> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var kind = FileGuard.DetectResumeKind(fileName, bytes.AsSpan(0, Math.Min(8, bytes.Length)), bytes.LongLength);

        var links = new List<string>();
        var text = kind switch
        {
            "pdf" => ExtractPdf(bytes, links),
            "docx" => ExtractDocx(bytes, links),
            _ => DecodeText(bytes)
        };

        text = Normalize(text);
        if (text.Length < 80)
        {
            throw new BusinessException(
                "Dosyadan okunabilir metin çıkarılamadı. CV'niz taranmış bir görsel olabilir; lütfen metin seçilebilir bir PDF veya DOCX yükleyin.");
        }

        var cleanLinks = links
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase) || l.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();

        return new ExtractedDocument(text, cleanLinks);
    }

    private static string ExtractPdf(byte[] bytes, List<string> links)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(bytes);
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(ContentOrderTextExtractor.GetText(page));
            sb.AppendLine();

            try
            {
                links.AddRange(page.GetHyperlinks().Select(h => h.Uri).Where(u => !string.IsNullOrWhiteSpace(u))!);
            }
            catch (Exception)
            {
                // Bozuk bağlantı açıklamaları metin çıkarmayı engellemesin.
            }
        }

        return sb.ToString();
    }

    private static string ExtractDocx(byte[] bytes, List<string> links)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        if (doc.MainDocumentPart is { } main)
        {
            links.AddRange(main.HyperlinkRelationships.Where(r => r.IsExternal).Select(r => r.Uri.ToString()));
        }

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
