using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using static OzgecmisAnalizi.Business.Jobs.JobJson;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>
/// Remotive herkese açık API'si (anahtar gerekmez): https://github.com/remotive-com/remote-jobs-api
/// Koşullar: günde en fazla birkaç istek, ilan Remotive'e bağlantı vererek ve kaynak belirtilerek gösterilir.
/// Bu yüzden besleme tek istekte alınır, <see cref="RemotiveOptions.CacheHours"/> saat önbellekte tutulur ve yerelde süzülür.
/// </summary>
public sealed class RemotiveProvider(HttpClient http, IMemoryCache cache, IOptions<JobSearchOptions> options) : IJobProvider
{
    private const string CacheKey = "jobs:remotive:feed";
    private readonly JobSearchOptions _all = options.Value;
    private RemotiveOptions O => _all.Remotive;

    public string Name => "Remotive";
    public bool IsEnabled => O.Enabled;
    public JobProviderScope Scope => JobProviderScope.Remote;
    public string Homepage => "https://remotive.com";

    public async Task<IReadOnlyList<RawJob>> SearchAsync(JobQuery query, CancellationToken ct)
    {
        if (query.Page > 1)
        {
            return []; // Besleme tek sayfadır; ilk sayfada tümü gösterilir.
        }

        var feed = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, O.CacheHours));
            return await FetchAsync(ct);
        }) ?? [];

        return RemoteJobFilter.Apply(feed, query, _all.RemoteRegions, O.MaxResults);
    }

    private async Task<IReadOnlyList<RawJob>> FetchAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync(O.BaseUrl, ct);
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
            var url = Str(j, "url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            result.Add(new RawJob(
                "remotive-" + (Str(j, "id") ?? url.GetHashCode().ToString()),
                Plain(Str(j, "title"), 160) ?? "İlan",
                Str(j, "company_name") ?? "Belirtilmemiş",
                Join("Uzaktan", Str(j, "candidate_required_location")),
                Plain(Str(j, "description")),
                url,
                "Remotive",
                Date(Str(j, "publication_date")),
                Str(j, "salary") is { Length: > 0 } s ? s : null,
                Str(j, "job_type")?.Replace('_', ' '),
                IsRemote: true));
        }

        return result;
    }
}
