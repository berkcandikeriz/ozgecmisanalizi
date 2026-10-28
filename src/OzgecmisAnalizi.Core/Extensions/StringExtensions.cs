using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OzgecmisAnalizi.Core.Extensions;

public static partial class StringExtensions
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Karşılaştırma için metni katlar: Türkçe küçük harf, aksan/diakritik kaldırma, tek boşluk.
    /// "İş Analisti" ve "is analisti" aynı sonucu verir.
    /// </summary>
    public static string ToSearchKey(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = value.ToLower(Tr)
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');

        var normalized = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return MultiSpace().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>URL dostu kısa ad üretir: "Yazılım Geliştirici" → "yazilim-gelistirici".</summary>
    public static string ToSlug(this string? value)
    {
        var key = value.ToSearchKey();
        var slug = NonSlug().Replace(key, "-").Trim('-');
        return MultiDash().Replace(slug, "-");
    }

    public static string Truncate(this string? value, int maxLength, string suffix = "…")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return string.Concat(value.AsSpan(0, Math.Max(0, maxLength - suffix.Length)), suffix);
    }

    public static string ToTitleCaseTr(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Tr.TextInfo.ToTitleCase(value.ToLower(Tr));

    public static bool HasValue(this string? value) => !string.IsNullOrWhiteSpace(value);

    public static int WordCount(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? 0 : Words().Matches(value).Count;

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlug();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDash();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'’\-\.+#]*")]
    private static partial Regex Words();
}
