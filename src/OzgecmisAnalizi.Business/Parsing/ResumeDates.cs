using System.Text.RegularExpressions;
using OzgecmisAnalizi.Core.Extensions;

namespace OzgecmisAnalizi.Business.Parsing;

/// <summary>CV'lerdeki tarih ifadelerini (Türkçe/İngilizce) çözümler.</summary>
public static partial class ResumeDates
{
    private static readonly Dictionary<string, int> Months = new()
    {
        ["ocak"] = 1, ["oca"] = 1, ["jan"] = 1, ["january"] = 1,
        ["subat"] = 2, ["sub"] = 2, ["feb"] = 2, ["february"] = 2,
        ["mart"] = 3, ["mar"] = 3, ["march"] = 3,
        ["nisan"] = 4, ["nis"] = 4, ["apr"] = 4, ["april"] = 4,
        ["mayis"] = 5, ["may"] = 5,
        ["haziran"] = 6, ["haz"] = 6, ["jun"] = 6, ["june"] = 6,
        ["temmuz"] = 7, ["tem"] = 7, ["jul"] = 7, ["july"] = 7,
        ["agustos"] = 8, ["agu"] = 8, ["aug"] = 8, ["august"] = 8,
        ["eylul"] = 9, ["eyl"] = 9, ["sep"] = 9, ["sept"] = 9, ["september"] = 9,
        ["ekim"] = 10, ["eki"] = 10, ["oct"] = 10, ["october"] = 10,
        ["kasim"] = 11, ["kas"] = 11, ["nov"] = 11, ["november"] = 11,
        ["aralik"] = 12, ["ara"] = 12, ["dec"] = 12, ["december"] = 12
    };

    private static readonly string[] CurrentWords = ["gunumuz", "halen", "devam", "present", "current", "now", "simdi", "suan", "hala"];

    public const string DatePattern =
        @"(?:(?:0?[1-9]|1[0-2])[./\-](?:19|20)\d{2}|(?:19|20)\d{2}[./\-](?:0?[1-9]|1[0-2])|[A-Za-zÇĞİÖŞÜçğıöşü]{3,9}\.?\s+(?:19|20)\d{2}|(?:19|20)\d{2})";

    public const string CurrentPattern =
        @"(?:günümüz|gunumuz|halen|devam\s*ediyor|devam|present|current|now|şu\s*an|hâlâ|hala)";

    [GeneratedRegex(@"(?<start>" + DatePattern + @")\s*(?:[-–—]|to|ile|\s)\s*(?<end>" + DatePattern + "|" + CurrentPattern + ")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex RangeRegex();

    [GeneratedRegex(@"(?:19|20)\d{2}")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^(?<m>0?[1-9]|1[0-2])[./\-](?<y>(?:19|20)\d{2})$")]
    private static partial Regex MonthYear();

    [GeneratedRegex(@"^(?<y>(?:19|20)\d{2})[./\-](?<m>0?[1-9]|1[0-2])$")]
    private static partial Regex YearMonth();

    [GeneratedRegex(@"^(?<name>[a-z]{3,9})\.?\s+(?<y>(?:19|20)\d{2})$")]
    private static partial Regex NamedMonthYear();

    public static bool IsCurrent(string? value)
    {
        var key = value.ToSearchKey();
        return key.Length > 0 && CurrentWords.Any(w => key.Contains(w));
    }

    /// <summary>"03/2021", "2021-03", "Mart 2021", "2021" → ilk gün. Çözülemezse null.</summary>
    public static DateOnly? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var key = value.ToSearchKey().Trim();
        Match m;
        if ((m = MonthYear().Match(key)).Success || (m = YearMonth().Match(key)).Success)
        {
            return new DateOnly(int.Parse(m.Groups["y"].Value), int.Parse(m.Groups["m"].Value), 1);
        }

        if ((m = NamedMonthYear().Match(key)).Success && Months.TryGetValue(m.Groups["name"].Value, out var month))
        {
            return new DateOnly(int.Parse(m.Groups["y"].Value), month, 1);
        }

        var year = YearRegex().Match(key);
        return year.Success ? new DateOnly(int.Parse(year.Value), 1, 1) : null;
    }

    /// <summary>Başlangıç-bitiş arası yıl cinsinden süre; bitiş yoksa/devam ediyorsa bugüne kadar.</summary>
    public static double YearsBetween(string? start, string? end, bool isCurrent)
    {
        var s = Parse(start);
        if (s is null)
        {
            return 0;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var e = isCurrent || IsCurrent(end) ? today : Parse(end) ?? s.Value.AddYears(1);
        if (e > today)
        {
            e = today;
        }

        var months = (e.Year - s.Value.Year) * 12 + e.Month - s.Value.Month;
        // Yalnızca yıl yazılmış ve aynı yılsa en az birkaç ay sayalım.
        return Math.Max(months, 1) / 12.0;
    }
}
