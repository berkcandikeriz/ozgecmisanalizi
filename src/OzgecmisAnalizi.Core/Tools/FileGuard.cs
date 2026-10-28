using OzgecmisAnalizi.Core.Exceptions;

namespace OzgecmisAnalizi.Core.Tools;

/// <summary>Yüklenen dosyalar için uzantı, boyut ve imza (magic number) kontrolleri.</summary>
public static class FileGuard
{
    public const long MaxResumeBytes = 10 * 1024 * 1024;

    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    public static string DetectResumeKind(string fileName, ReadOnlySpan<byte> head, long length)
    {
        if (length <= 0)
        {
            throw new ValidationException("Dosya boş.");
        }

        if (length > MaxResumeBytes)
        {
            throw new ValidationException("Dosya 10 MB sınırını aşıyor.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" when head.StartsWith(PdfMagic) => "pdf",
            ".docx" when head.StartsWith(ZipMagic) => "docx",
            ".txt" => "txt",
            ".pdf" or ".docx" => throw new ValidationException("Dosya içeriği uzantısıyla uyuşmuyor."),
            _ => throw new ValidationException("Yalnızca PDF, DOCX veya TXT dosyaları yüklenebilir.")
        };
    }
}
