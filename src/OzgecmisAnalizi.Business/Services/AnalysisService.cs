using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Ai;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Business.Pdf;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Core.Tools;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.Business.Services;

public sealed class AnalysisService(
    IUnitOfWork uow,
    ICurrentUser currentUser,
    ISkillCatalog catalog,
    IResumeAnalyzer analyzer,
    IAiProvider ai,
    IAnalysisReportRenderer reportRenderer,
    ILogger<AnalysisService> logger) : IAnalysisService
{
    private IRepository<ResumeAnalysis> Analyses => uow.Repository<ResumeAnalysis>();

    public async Task<AnalysisDto> AnalyzeAsync(Guid resumeId, AnalyzeRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var resume = await uow.Repository<Resume>().Tracked()
            .FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId, ct);
        Guard.Found(resume, "CV", resumeId);

        // Yüklenen CV'de ham metin, elle düzenlenmiş CV'de yapılandırılmış içerik esas alınır.
        var composed = ResumeTextComposer.Compose(resume!.Content);
        var text = resume.Source == Entities.Enums.ResumeSource.Upload && resume.RawText.HasValue() && resume.UpdatedAt is null
            ? resume.RawText!
            : composed;

        var professions = await catalog.GetAsync(ct);
        var professionId = request.ProfessionId ?? resume.TargetProfessionId;
        var outcome = analyzer.Analyze(resume.Content, text, professions, professionId, request.City);

        var engine = "Kural motoru";
        if (request.UseAi && ai.IsEnabled)
        {
            var reply = AiJson.TryParse<AiAnalysisResponse>(await ai.CompleteAsync(
                AiPrompts.System,
                AiPrompts.Analysis(text.Truncate(12000), outcome.Profession?.Name, outcome.Overall, outcome.Details.MissingSkills.Select(m => m.Skill)),
                jsonOutput: true,
                ct));

            if (reply is not null)
            {
                MergeAi(outcome, reply);
                engine = "Kural motoru + " + ai.Name;
            }
            else
            {
                logger.LogInformation("Yapay zeka yanıtı alınamadı; yalnızca kural motoru sonucu kaydedildi.");
            }
        }

        if (outcome.Profession is not null && resume.TargetProfessionId is null)
        {
            resume.TargetProfessionId = outcome.Profession.Id;
        }

        var entity = new ResumeAnalysis
        {
            UserId = userId,
            ResumeId = resume.Id,
            ProfessionId = outcome.Profession?.Id,
            OverallScore = outcome.Overall,
            ComprehensivenessScore = outcome.Comprehensiveness,
            StructureScore = outcome.Structure,
            ClarityScore = outcome.Clarity,
            AtsScore = outcome.Ats,
            SkillMatchScore = outcome.SkillMatch,
            Label = outcome.Label,
            TopPercent = outcome.TopPercent,
            Engine = engine,
            Details = outcome.Details
        };

        await Analyses.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return entity.ToDto(resume.Title, outcome.Profession?.Name);
    }

    public async Task<AnalysisDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var row = await Analyses.Query()
            .Where(a => a.Id == id && a.UserId == userId)
            .Select(a => new { Analysis = a, ResumeTitle = a.Resume!.Title, ProfessionName = a.Profession != null ? a.Profession.Name : null })
            .FirstOrDefaultAsync(ct);
        Guard.Found(row, "Analiz", id);
        return row!.Analysis.ToDto(row.ResumeTitle, row.ProfessionName);
    }

    public async Task<IReadOnlyList<AnalysisListItemDto>> ListAsync(Guid? resumeId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var query = Analyses.Query().Where(a => a.UserId == userId);
        if (resumeId is not null)
        {
            query = query.Where(a => a.ResumeId == resumeId);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new AnalysisListItemDto(a.Id, a.ResumeId, a.Resume!.Title, a.Profession != null ? a.Profession.Name : null, a.OverallScore, a.Label, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<(byte[] Content, string FileName)> RenderReportAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await GetAsync(id, ct);
        var owner = await uow.Repository<User>().Query().Where(u => u.Id == currentUser.RequireId()).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var bytes = reportRenderer.Render(dto, owner);
        return (bytes, $"cv-analiz-raporu-{dto.CreatedAt:yyyyMMdd}.pdf");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var entity = await Analyses.Tracked().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        Analyses.Remove(Guard.Found(entity, "Analiz", id));
        await uow.SaveChangesAsync(ct);
    }

    private static void MergeAi(AnalysisOutcome outcome, AiAnalysisResponse reply)
    {
        var d = outcome.Details;
        d.AiSummary = reply.Summary?.Truncate(2000);

        if (reply.Strengths is { Count: > 0 })
        {
            d.Strengths = reply.Strengths.Concat(d.Strengths).Where(s => s.HasValue()).Distinct().Take(6).ToList();
        }

        if (reply.Improvements is { Count: > 0 })
        {
            d.Improvements = reply.Improvements.Concat(d.Improvements).Where(s => s.HasValue()).Distinct().Take(10).ToList();
        }

        if (reply.SectionTips is null)
        {
            return;
        }

        foreach (var (section, tips) in reply.SectionTips)
        {
            var target = d.Sections.FirstOrDefault(s => s.Section.ToSearchKey() == section.ToSearchKey());
            if (target is not null && tips is { Count: > 0 })
            {
                target.Tips = tips.Take(3).Concat(target.Tips).Distinct().Take(6).ToList();
            }
        }
    }
}
