using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Options;
using static OzgecmisAnalizi.Business.Jobs.JobJson;

namespace OzgecmisAnalizi.Business.Jobs;

/// <summary>
/// Careerjet arama API'si v4 (https://search.api.careerjet.net/v4/query). Türkiye (tr_TR) dahil 90+ ülkede,
/// Kariyer.net, Yenibiris, Eleman.net gibi sitelerden ve şirket sayfalarından toplanan ilanları döndürür.
/// Ücretsiz yayıncı hesabıyla alınan API anahtarı gerekir (HTTP Basic: anahtar kullanıcı adı, parola boş).
/// API son kullanıcının IP ve tarayıcı bilgisini zorunlu ister; bunlar WebUI'den X-Forwarded-For ile iletilir.
/// </summary>
public sealed class CareerjetProvider(HttpClient http, IMemoryCache cache, IOptions<JobSearchOptions> options, ILogger<CareerjetProvider> logger)
    : IJobProvider
{
    private readonly CareerjetOptions _o = options.Value.Careerjet;

    public string Name => "Careerjet";
    public bool IsEnabled => _o.Enabled;
    public JobProviderScope Scope => JobProviderScope.Turkey;
    public string Homepage => "https://www.careerjet.com.tr";

    public async Task<IReadOnlyList<RawJob>> SearchAsync(JobQuery query, CancellationToken ct)
    {
        var location = IsCountryWide(query.Location) ? string.Empty : query.Location!;
        var key = $"jobs:careerjet:{query.Keywords.ToLowerInvariant()}|{location.ToLowerInvariant()}|{query.Page}";
        return await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(5, _o.CacheMinutes));
            var jobs = await FetchAsync(query, location, ct);
            // Belirsiz konum ("LOCATIONS" yanıtı) veya sonuç yoksa ülke genelinde yeniden denenir.
            if (jobs.Count == 0 && location.Length > 0)
            {
                jobs = await FetchAsync(query, string.Empty, ct);
            }

            return jobs;
        }) ?? [];
    }

    private async Task<IReadOnlyList<RawJob>> FetchAsync(JobQuery query, string location, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["locale_code"] = _o.LocaleCode,
            ["keywords"] = query.Keywords,
            ["location"] = location,
            ["page"] = Math.Clamp(query.Page, 1, 10).ToString(),
            ["page_size"] = Math.Clamp(_o.PageSize, 1, 100).ToString(),
            ["sort"] = "relevance",
            ["fragment_size"] = "300",
            ["user_ip"] = query.ClientIp ?? _o.FallbackUserIp,
            ["user_agent"] = query.UserAgent ?? "Mozilla/5.0 (OzgecmisAnalizi)"
        };
        var url = _o.BaseUrl + "?" + string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(_o.ApiKey + ":")));
        if (!string.IsNullOrWhiteSpace(_o.Referer))
        {
            request.Headers.Referrer = new Uri(_o.Referer);
        }

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Careerjet {Status} döndü: {Body}", (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var type = Str(root, "type");
        if (string.Equals(type, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Careerjet hata: {Error}", Str(root, "error") ?? Str(root, "message") ?? body);
            throw new HttpRequestException("Careerjet: " + (Str(root, "error") ?? Str(root, "message") ?? "bilinmeyen hata"));
        }

        if (!root.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
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

            var site = Str(j, "site");
            result.Add(new RawJob(
                "careerjet-" + link.GetHashCode().ToString("x"),
                Plain(Str(j, "title"), 160) ?? "İlan",
                Str(j, "company") is { Length: > 0 } company ? company : "Belirtilmemiş",
                Str(j, "locations"),
                Plain(Str(j, "description")),
                link,
                "Careerjet" + (site is { Length: > 0 } ? " · " + site : string.Empty),
                Date(Str(j, "date")),
                Str(j, "salary") is { Length: > 0 } salary ? salary : null,
                null));
        }

        return result;
    }

    private static bool IsCountryWide(string? location) =>
        string.IsNullOrWhiteSpace(location) || location.Trim().ToLowerInvariant() is "türkiye" or "turkiye" or "turkey" or "tümü";
}
