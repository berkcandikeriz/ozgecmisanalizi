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

namespace OzgecmisAnalizi.Business.Services;

/// <summary>
/// CV'ye göre iş arar: etkin ilan API'lerini paralel sorgular, her ilanı CV becerileriyle puanlar ve
/// LinkedIn / Indeed / Kariyer.net vb. için hazır arama bağlantıları ekler.
/// </summary>
public sealed class JobSearchService(
    IEnumerable<IJobProvider> providers,
    JobSiteLinkBuilder linkBuilder,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    ISkillCatalog catalog,
    IOptions<JobSearchOptions> options,
    ILogger<JobSearchService> logger) : IJobSearchService
{
    public async Task<JobSearchResultDto> SearchAsync(JobSearchQuery query, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var warnings = new List<string>();
        var skills = new List<string>();
        var keywords = query.Keywords.HasValue() ? query.Keywords!.Trim() : null;
        var location = query.Location.HasValue() ? query.Location!.Trim() : null;

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
        var active = providers.Where(p => p.IsEnabled).ToList();
        if (active.Count == 0)
        {
            warnings.Add("Henüz bir ilan API'si yapılandırılmadı. Aşağıdaki site bağlantıları her zaman çalışır; " +
                         "doğrudan ilan listesi için appsettings içinde JobSearch:Jooble:ApiKey veya JobSearch:JSearch:ApiKey girin.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));

        var tasks = active.Select(async p =>
        {
            try
            {
                return await p.SearchAsync(keywords!, location, Math.Max(1, query.Page), timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                logger.LogWarning(ex, "{Provider} ilan araması başarısız.", p.Name);
                lock (warnings)
                {
                    warnings.Add($"{p.Name} şu anda yanıt vermiyor.");
                }

                return (IReadOnlyList<RawJob>)Array.Empty<RawJob>();
            }
        });

        var raw = (await Task.WhenAll(tasks)).SelectMany(x => x).ToList();
        var catalogSkills = await catalog.AllSkillsAsync(ct);
        var skillKeys = skills
            .Select(s => (Name: s, Keys: catalogSkills.FirstOrDefault(c => c.Name.ToSearchKey() == s.ToSearchKey())?.Keys ?? new[] { SkillMatcher.NormalizeKey(s) }))
            .ToList();
        var keywordTokens = SkillMatcher.NormalizeKey(keywords!).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 1).ToList();

        var listings = raw
            .GroupBy(j => (j.Title.ToSearchKey(), j.Company.ToSearchKey()))
            .Select(g => g.First())
            .Select(j => Score(j, skillKeys, keywordTokens))
            .OrderByDescending(j => j.MatchScore)
            .ThenByDescending(j => j.PostedAt)
            .Take(60)
            .ToList();

        return new JobSearchResultDto(keywords!, location, listings, linkBuilder.Build(keywords!, location),
            active.Select(p => p.Name).ToList(), warnings);
    }

    private static JobListingDto Score(RawJob j, List<(string Name, IReadOnlyList<string> Keys)> skills, List<string> keywordTokens)
    {
        var title = SkillMatcher.PrepareText(j.Title);
        var body = SkillMatcher.PrepareText(j.Title + " " + j.Snippet);

        var titleHit = keywordTokens.Count == 0 ? 0 : keywordTokens.Count(t => SkillMatcher.Contains(title, t)) / (double)keywordTokens.Count;
        var matched = skills.Where(s => SkillMatcher.ContainsAny(body, s.Keys)).Select(s => s.Name).Take(8).ToList();
        var skillHit = skills.Count == 0 ? 0.5 : Math.Min(1, matched.Count / (double)Math.Min(6, skills.Count));

        var score = (int)Math.Round(100 * (0.5 * titleHit + 0.5 * skillHit));
        return new JobListingDto(j.Id, j.Title, j.Company, j.Location, j.Snippet, j.Url, j.Source, j.PostedAt, j.Salary,
            j.EmploymentType, Math.Clamp(score, 5, 99), matched);
    }
}
