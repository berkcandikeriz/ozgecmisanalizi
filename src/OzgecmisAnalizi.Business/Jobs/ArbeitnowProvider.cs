using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using static OzgecmisAnalizi.Business.Jobs.JobJson;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>
/// Arbeitnow herkese açık iş ilanı API'si (anahtar gerekmez): https://www.arbeitnow.com/blog/job-board-api
/// Ağırlıklı olarak Avrupa (Almanya) ilanlarıdır; bu yüzden yalnızca uzaktan (remote) işaretli ilanlar alınır.
/// İlk birkaç sayfa <see cref="ArbeitnowOptions.CacheHours"/> saat önbellekte tutulur ve yerelde süzülür.
/// </summary>
public sealed class ArbeitnowProvider(HttpClient http, IMemoryCache cache, IOptions<JobSearchOptions> options) : IJobProvider
{
    private const string CacheKey = "jobs:arbeitnow:feed";
    private readonly JobSearchOptions _all = options.Value;
    private ArbeitnowOptions O => _all.Arbeitnow;

    public string Name => "Arbeitnow";
    public bool IsEnabled => O.Enabled;
    public JobProviderScope Scope => JobProviderScope.Remote;
    public string Homepage => "https://www.arbeitnow.com";

    public async Task<IReadOnlyList<RawJob>> SearchAsync(JobQuery query, CancellationToken ct)
    {
        if (query.Page > 1)
        {
            return [];
        }

        var feed = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, O.CacheHours));
            return await FetchAsync(ct);
        }) ?? [];

        // Arbeitnow konumları şehir adıdır (Berlin...), bölge süzgeci yerine "remote" bayrağı kullanılır.
        return RemoteJobFilter.Apply(feed, query, [], O.MaxResults);
    }

    private async Task<IReadOnlyList<RawJob>> FetchAsync(CancellationToken ct)
    {
        var result = new List<RawJob>();
        for (var page = 1; page <= Math.Clamp(O.Pages, 1, 5); page++)
        {
            using var response = await http.GetAsync($"{O.BaseUrl}?page={page}", ct);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var j in data.EnumerateArray())
            {
                var url = Str(j, "url");
                var remote = j.TryGetProperty("remote", out var r) && r.ValueKind == JsonValueKind.True;
                if (string.IsNullOrWhiteSpace(url) || !remote)
                {
                    continue;
                }

                result.Add(new RawJob(
                    "arbeitnow-" + (Str(j, "slug") ?? url.GetHashCode().ToString()),
                    Plain(Str(j, "title"), 160) ?? "İlan",
                    Str(j, "company_name") ?? "Belirtilmemiş",
                    Join("Uzaktan", Str(j, "location")),
                    Plain(Str(j, "description")),
                    url,
                    "Arbeitnow",
                    Date(Str(j, "created_at")),
                    null,
                    string.Join(", ", StrList(j, "job_types")) is { Length: > 0 } type ? type : null,
                    IsRemote: true));
            }
        }

        return result;
    }
}
