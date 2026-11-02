using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using static OzgecmisAnalizi.Business.Jobs.JobJson;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>
/// Jobicy herkese açık API'si (v2, anahtar gerekmez): https://github.com/Jobicy/remote-jobs-api
/// Koşullar: saatte bir sorgudan sık çağrılmaz, ilan Jobicy bağlantısıyla ve kaynak belirtilerek gösterilir.
/// Her anahtar kelime için sonuç <see cref="JobicyOptions.CacheHours"/> saat önbellekte tutulur.
/// </summary>
public sealed class JobicyProvider(HttpClient http, IMemoryCache cache, IOptions<JobSearchOptions> options) : IJobProvider
{
    private readonly JobSearchOptions _all = options.Value;
    private JobicyOptions O => _all.Jobicy;

    public string Name => "Jobicy";
    public bool IsEnabled => O.Enabled;
    public JobProviderScope Scope => JobProviderScope.Remote;
    public string Homepage => "https://jobicy.com";

    public async Task<IReadOnlyList<RawJob>> SearchAsync(JobQuery query, CancellationToken ct)
    {
        if (query.Page > 1)
        {
            return [];
        }

        // Jobicy "tag" araması İngilizce unvanlarla daha iyi sonuç verir; katalogdaki ilk unvan tercih edilir.
        var tag = query.AltKeywords.FirstOrDefault(IsAscii) ?? query.Keywords;
        var feed = await cache.GetOrCreateAsync("jobs:jobicy:" + tag.Trim().ToLowerInvariant(), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, O.CacheHours));
            return await FetchAsync(tag, ct);
        }) ?? [];

        return RemoteJobFilter.Apply(feed, query, _all.RemoteRegions, O.MaxResults);
    }

    private async Task<IReadOnlyList<RawJob>> FetchAsync(string tag, CancellationToken ct)
    {
        var url = $"{O.BaseUrl}?count={Math.Clamp(O.Count, 1, 100)}&tag={Uri.EscapeDataString(tag)}";
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<RawJob>();
        foreach (var j in jobs.EnumerateArray())
        {
            var link = Str(j, "url");
            if (string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var min = Str(j, "annualSalaryMin") ?? Str(j, "salaryMin");
            var max = Str(j, "annualSalaryMax") ?? Str(j, "salaryMax");
            var currency = Str(j, "salaryCurrency");
            var salary = min is null && max is null ? null : $"{min ?? "?"}–{max ?? "?"} {currency} / yıl".Trim();

            result.Add(new RawJob(
                "jobicy-" + (Str(j, "id") ?? link.GetHashCode().ToString()),
                Plain(Str(j, "jobTitle"), 160) ?? "İlan",
                Str(j, "companyName") ?? "Belirtilmemiş",
                Join("Uzaktan", Str(j, "jobGeo")),
                Plain(Str(j, "jobExcerpt") ?? Str(j, "jobDescription")),
                link,
                "Jobicy",
                Date(Str(j, "pubDate")),
                salary,
                string.Join(", ", StrList(j, "jobType")) is { Length: > 0 } type ? type : null,
                IsRemote: true));
        }

        return result;
    }

    private static bool IsAscii(string s) => s.All(c => c < 128);
}
