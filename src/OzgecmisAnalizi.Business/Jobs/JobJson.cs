using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>İlan API yanıtlarını okumak için ortak yardımcılar.</summary>
internal static partial class JobJson
{
    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()
            : null;

    public static IEnumerable<string> StrList(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
        {
            yield break;
        }

        if (v.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in v.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } s)
                {
                    yield return s;
                }
            }
        }
        else if (v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } single)
        {
            yield return single;
        }
    }

    public static DateTime? Date(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, out var unix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        }

        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d.UtcDateTime : null;
    }

    /// <summary>HTML etiketlerini atar, varlıkları çözer, boşlukları sadeleştirir ve kısaltır.</summary>
    public static string? Plain(string? html, int max = 400)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(TagRegex().Replace(html, " "));
        text = SpaceRegex().Replace(text, " ").Trim();
        return text.Length > max ? text[..max].TrimEnd() + "…" : text;
    }

    public static string? Join(params string?[] parts)
    {
        var s = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return s.Length == 0 ? null : s;
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();
}
