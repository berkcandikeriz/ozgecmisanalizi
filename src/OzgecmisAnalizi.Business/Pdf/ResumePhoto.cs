using System.Text.RegularExpressions;

namespace OzgecmisAnalizi.Business.Pdf;

/// <summary>
/// Vesikalık fotoğraf: CV içeriğinde "data:image/jpeg;base64,..." biçiminde saklanır.
/// Tarayıcı fotoğrafı yüklemeden önce 3:4 oranında kırpıp 300×400 piksele küçültür (~30–60 KB),
/// böylece veritabanında ayrı tablo/migration gerekmez ve CV kopyalanınca fotoğraf da kopyalanır.
///
/// ATS notu: PDF'te fotoğraf metinden bağımsız bir görsel olarak basılır; ad soyad ilk satırda düz metin kalır,
/// metin kutusu/arka plan kullanılmaz ve dosya küçük tutulur. Bu koşullarda ATS metin çıkarımı etkilenmez.
/// </summary>
public static partial class ResumePhoto
{
    /// <summary>Base64 dahil en fazla karakter (~225 KB görsel).</summary>
    public const int MaxDataUrlLength = 300_000;

    [GeneratedRegex(@"^data:image/(jpeg|jpg|png);base64,([A-Za-z0-9+/=\s]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DataUrl();

    /// <summary>Geçerli bir JPEG/PNG data URL'si ise görsel baytlarını döndürür.</summary>
    public static bool TryDecode(string? dataUrl, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(dataUrl) || dataUrl.Length > MaxDataUrlLength)
        {
            return false;
        }

        var match = DataUrl().Match(dataUrl.Trim());
        if (!match.Success)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(match.Groups[2].Value);
        }
        catch (FormatException)
        {
            return false;
        }

        return IsJpeg(bytes) || IsPng(bytes);
    }

    /// <summary>Kaydetmeden önce temizler: geçersiz, çok büyük ya da görsel olmayan veri null olur.</summary>
    public static string? Normalize(string? dataUrl) =>
        TryDecode(dataUrl, out var bytes)
            ? $"data:image/{(IsPng(bytes) ? "png" : "jpeg")};base64,{Convert.ToBase64String(bytes)}"
            : null;

    private static bool IsJpeg(byte[] b) => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    private static bool IsPng(byte[] b) => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
}
