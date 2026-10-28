using System.Text.Json;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>
/// JSearch (RapidAPI) — Google for Jobs verisini kullanır; LinkedIn, Indeed, Glassdoor ve şirket sitelerindeki
/// ilanları resmi bir API üzerinden döndürür.
/// </summary>
public sealed class JSearchProvider(HttpClient http, IOptions<JobSearchOptions> options) : IJobProvider
{
    private readonly JSearchOptions _o = options.Value.JSearch;

    public string Name => "JSearch";
    public bool IsEnabled => _o.Enabled;

    public async Task<IReadOnlyList<RawJob>> SearchAsync(string keywords, string? location, int page, CancellationToken ct)
    {
        var query = string.IsNullOrWhiteSpace(location) ? keywords : $"{keywords} {location}";
        var url = $"https://{_o.Host}/search?query={Uri.EscapeDataString(query)}&page={page}&num_pages=1&country={_o.Country}&date_posted=month";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-RapidAPI-Key", _o.ApiKey);
        request.Headers.Add("X-RapidAPI-Host", _o.Host);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<RawJob>();
        foreach (var j in data.EnumerateArray())
        {
            var link = Str(j, "job_apply_link") ?? Str(j, "job_google_link");
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var city = Str(j, "job_city");
            var description = Str(j, "job_description");
            result.Add(new RawJob(
                "jsearch-" + (Str(j, "job_id") ?? link.GetHashCode().ToString()),
                Str(j, "job_title") ?? "İlan",
                Str(j, "employer_name") ?? "Belirtilmemiş",
                string.Join(", ", new[] { city, Str(j, "job_state") }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } loc ? loc : Str(j, "job_country"),
                description is null ? null : description.Length > 400 ? description[..400] + "…" : description,
                link,
                Str(j, "job_publisher") ?? "JSearch",
                DateTime.TryParse(Str(j, "job_posted_at_datetime_utc"), out var posted) ? posted : null,
                null,
                Str(j, "job_employment_type")));
        }

        return result;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()
            : null;
}
