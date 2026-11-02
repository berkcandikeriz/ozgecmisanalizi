using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Business.Jobs;
using OzgecmisAnalizi.Business.Options;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Services;

/// <summary>
/// CV'ye göre iş arar: etkin ilan API'lerini (Careerjet, Jooble, JSearch; anahtarsız Remotive, Jobicy, Arbeitnow)
/// paralel sorgular, her ilanı CV becerileriyle puanlar ve herkese açık API'si olmayan LinkedIn / Indeed /
/// Kariyer.net / Yenibiris / Eleman.net için hazır arama bağlantıları ekler (kazıma yapılmaz).
/// </summary>
public sealed class JobSearchService(
    IEnumerable<IJobProvider> providers,
    JobSiteLinkBuilder linkBuilder,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    ISkillCatalog catalog,
    IHttpContextAccessor http,
    IOptions<JobSearchOptions> options,
    ILogger<JobSearchService> logger) : IJobSearchService
{
    /// <summary>Herkese açık arama API'si olmayan, yalnızca site bağlantısıyla desteklenen platformlar.</summary>
    private const string NoPublicApiSites = "LinkedIn, Indeed, Kariyer.net, Yenibiris ve Eleman.net";

    public async Task<JobSearchResultDto> SearchAsync(JobSearchQuery query, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var warnings = new List<string>();
        var notices = new List<string>();
        var skills = new List<string>();
        var keywords = query.Keywords.HasValue() ? query.Keywords!.Trim() : null;
        var location = query.Location.HasValue() ? query.Location!.Trim() : null;
        var includeRemote = query.IncludeRemote ?? options.Value.IncludeRemoteByDefault;
        Guid? professionId = null;

        if (query.ResumeId is { } resumeId)
        {
            var resume = await uow.Repository<Resume>().Query()
                .Include(r => r.TargetProfession)
                .FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId, ct)
                ?? throw new NotFoundException("CV", resumeId);

            var lastAnalysis = await uow.Repository<ResumeAnalysis>().Query()
                .Where(a => a.ResumeId == resumeId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(ct);

            skills.AddRange(resume.Content.Skills);
            if (lastAnalysis is not null)
            {
                skills.AddRange(lastAnalysis.Details.DetectedSkills);
            }

            professionId = resume.TargetProfessionId ?? lastAnalysis?.ProfessionId;
            keywords ??= resume.TargetProfession?.SearchKeywords.Split(',').FirstOrDefault()?.Trim()
                         ?? resume.Content.Personal.Title
                         ?? resume.Content.Experiences.FirstOrDefault()?.Position;
            location ??= resume.Content.Personal.City;
        }

        if (!keywords.HasValue())
        {
            throw new ValidationException("Aranacak pozisyon veya anahtar kelime girin ya da bir CV seçin.");
        }

        skills = skills.DistinctBySearchKey();
        var altKeywords = await AltKeywordsAsync(keywords!, professionId, ct);

        var enabled = providers.Where(p => p.IsEnabled).ToList();
        var active = enabled.Where(p => includeRemote || p.Scope != JobProviderScope.Remote).ToList();
        if (!enabled.Any(p => p.Scope == JobProviderScope.Turkey))
        {
            notices.Add($"{NoPublicApiSites} herkese açık bir ilan arama API'si sunmuyor (yalnızca iş ortaklarına açık ya da kapatılmış), " +
                        "bu yüzden bu sitelerde arama için sağdaki bağlantılar kullanılır. Türkiye ilanlarını burada doğrudan listelemek için " +
                        "ücretsiz bir Careerjet (JobSearch:Careerjet:ApiKey) veya Jooble (JobSearch:Jooble:ApiKey) anahtarı ekleyin.");
        }

        if (active.Count == 0)
        {
            warnings.Add(enabled.Count == 0
                ? "Tüm ilan kaynakları kapalı. appsettings içindeki JobSearch bölümünden en az birini etkinleştirin."
                : "Uzaktan ilanlar kapalı ve Türkiye ilan kaynağı için anahtar girilmemiş. \"Uzaktan (yurt dışı) ilanları da göster\" seçeneğini açın.");
        }

        var context = http.HttpContext;
        var clientIp = context?.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim() is { Length: > 0 } forwarded
            ? forwarded
            : context?.Connection.RemoteIpAddress?.ToString();
        var userAgent = context?.Request.Headers["X-Client-User-Agent"].ToString() is { Length: > 0 } clientUa
            ? clientUa
            : context?.Request.Headers.UserAgent.ToString();
        var jobQuery = new JobQuery(keywords!, location, Math.Max(1, query.Page), altKeywords, clientIp, userAgent);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));

        var tasks = active.Select(async p =>
        {
            try
            {
                return (Provider: p, Jobs: await p.SearchAsync(jobQuery, timeout.Token));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "{Provider} ilan araması başarısız.", p.Name);
                lock (warnings)
                {
                    warnings.Add($"{p.Name} şu anda yanıt vermiyor; diğer kaynaklar gösteriliyor.");
                }

                return (Provider: p, Jobs: (IReadOnlyList<RawJob>)Array.Empty<RawJob>());
            }
        });

        var perProvider = await Task.WhenAll(tasks);
        var raw = perProvider.SelectMany(x => x.Jobs).ToList();
        var catalogSkills = await catalog.AllSkillsAsync(ct);
        var skillKeys = skills
            .Select(s => (Name: s, Keys: catalogSkills.FirstOrDefault(c => c.Name.ToSearchKey() == s.ToSearchKey())?.Keys ?? new[] { SkillMatcher.NormalizeKey(s) }))
            .ToList();
        var phrases = jobQuery.AllKeywords
            .Select(k => SkillMatcher.NormalizeKey(k).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 1).ToList())
            .Where(t => t.Count > 0)
            .ToList();

        // Kullanıcının kaydettiği ilanlar işaretlenir, "uygun değil" dedikleri bir daha gösterilmez.
        var saved = await uow.Repository<JobApplication>().Query()
            .Where(a => a.UserId == userId && a.Url != null)
            .Select(a => new { a.Url, a.Stage })
            .ToListAsync(ct);
        var dismissed = saved.Where(a => a.Stage == ApplicationStage.UygunDegil).Select(a => a.Url!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tracked = saved.Where(a => a.Stage != ApplicationStage.UygunDegil).Select(a => a.Url!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unique = raw
            .GroupBy(j => (j.Title.ToSearchKey(), j.Company.ToSearchKey()))
            .Select(g => g.First())
            .ToList();
        var hidden = unique.Count(j => dismissed.Contains(j.Url));

        var listings = unique
            .Where(j => !dismissed.Contains(j.Url))
            .Select(j => Score(j, skillKeys, phrases) with { IsTracked = tracked.Contains(j.Url), IsRemote = j.IsRemote })
            .OrderByDescending(j => j.MatchScore)
            .ThenByDescending(j => j.PostedAt)
            .ToList();

        var providerInfo = perProvider
            .Select(x => new JobProviderDto(x.Provider.Name, x.Provider.Homepage, x.Provider.Scope == JobProviderScope.Remote ? "Uzaktan" : "Türkiye",
                listings.Count(l => l.Source.StartsWith(x.Provider.Name, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        return new JobSearchResultDto(keywords!, location, listings, linkBuilder.Build(keywords!, location),
            active.Select(p => p.Name).ToList(), warnings, Math.Max(1, query.Page), hidden, providerInfo, notices, includeRemote);
    }

    /// <summary>
    /// Meslek kataloğundaki eş anlamlı / İngilizce unvanlar ("Yazılım Geliştirici" → "backend developer"...).
    /// Uzaktan ilan kaynakları İngilizce olduğundan eşleşme bu unvanlarla yapılır.
    /// </summary>
    private async Task<IReadOnlyList<string>> AltKeywordsAsync(string keywords, Guid? professionId, CancellationToken ct)
    {
        var professions = await catalog.GetAsync(ct);
        var key = SkillMatcher.NormalizeKey(keywords);
        var profession = professionId is { } id ? professions.FirstOrDefault(p => p.Id == id) : null;
        profession ??= professions.FirstOrDefault(p =>
            p.SearchKeywords.Any(k => SkillMatcher.NormalizeKey(k) == key) ||
            SkillMatcher.NormalizeKey(p.Name) == key ||
            (key.Length >= 4 && p.NameKeys.Any(n => n == key)));

        return profession?.SearchKeywords.Take(4).ToList() ?? [];
    }

    private static JobListingDto Score(RawJob j, List<(string Name, IReadOnlyList<string> Keys)> skills, List<List<string>> phrases)
    {
        var title = SkillMatcher.PrepareText(j.Title);
        var body = SkillMatcher.PrepareText(j.Title + " " + j.Snippet);

        // Yazılan anahtar kelime ya da katalog unvanlarından en iyi eşleşeni (Türkçe/İngilizce ilanlar için).
        var titleHit = phrases.Count == 0 ? 0 : phrases.Max(tokens => tokens.Count(t => SkillMatcher.Contains(title, t)) / (double)tokens.Count);
        var matched = skills.Where(s => SkillMatcher.ContainsAny(body, s.Keys)).Select(s => s.Name).Take(8).ToList();
        var skillHit = skills.Count == 0 ? 0.5 : Math.Min(1, matched.Count / (double)Math.Min(6, skills.Count));

        var score = (int)Math.Round(100 * (0.5 * titleHit + 0.5 * skillHit));
        return new JobListingDto(j.Id, j.Title, j.Company, j.Location, j.Snippet, j.Url, j.Source, j.PostedAt, j.Salary,
            j.EmploymentType, Math.Clamp(score, 5, 99), matched);
    }
}
