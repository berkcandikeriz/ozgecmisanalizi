using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>Jooble REST API (POST {BaseUrl}{key}). Türkiye'deki birçok kariyer sitesinin ilanlarını toplar.</summary>
public sealed partial class JoobleProvider(HttpClient http, IOptions<JobSearchOptions> options) : IJobProvider
{
    private readonly JoobleOptions _o = options.Value.Jooble;

    public string Name => "Jooble";
    public bool IsEnabled => _o.Enabled;

    public async Task<IReadOnlyList<RawJob>> SearchAsync(string keywords, string? location, int page, CancellationToken ct)
    {
        var url = _o.BaseUrl.TrimEnd('/') + "/" + _o.ApiKey;
        using var response = await http.PostAsJsonAsync(url, new { keywords, location = location ?? string.Empty, page = page.ToString() }, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<RawJob>();
        foreach (var j in jobs.EnumerateArray())
        {
            var link = Str(j, "link");
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            result.Add(new RawJob(
                "jooble-" + (Str(j, "id") ?? link.GetHashCode().ToString()),
                StripTags(Str(j, "title")) ?? "İlan",
                Str(j, "company") ?? Str(j, "source") ?? "Belirtilmemiş",
                Str(j, "location"),
                StripTags(Str(j, "snippet")),
                link,
                "Jooble" + (Str(j, "source") is { Length: > 0 } src ? $" · {src}" : string.Empty),
                DateTime.TryParse(Str(j, "updated"), out var updated) ? updated : null,
                Str(j, "salary"),
                Str(j, "type")));
        }

        return result;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;

    private static string? StripTags(string? html) =>
        html is null ? null : WebUtilityDecode(TagRegex().Replace(html, string.Empty)).Trim();

    private static string WebUtilityDecode(string value) => System.Net.WebUtility.HtmlDecode(value);

    [GeneratedRegex("<.*?>")]
    private static partial Regex TagRegex();
}
