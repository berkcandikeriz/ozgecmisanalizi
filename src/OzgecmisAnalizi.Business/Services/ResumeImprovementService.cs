using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Ai;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Core.Tools;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Services;

/// <summary>
/// "Önerileri uygula": analiz önerilerini CV'ye uygulayıp kaydetmeden önce değişiklik listesi (diff) olarak sunar,
/// kullanıcının seçtiklerini kaydeder ve isteğe bağlı olarak yeniden analiz eder.
/// </summary>
public sealed class ResumeImprovementService(
    IUnitOfWork uow,
    ICurrentUser currentUser,
    ISkillCatalog catalog,
    IResumeImprover improver,
    IAnalysisService analyses,
    IAiProvider ai,
    ILogger<ResumeImprovementService> logger) : IResumeImprovementService
{
    public async Task<ImprovementPreviewDto> PreviewAsync(Guid resumeId, Guid? analysisId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var resume = Guard.Found(
            await uow.Repository<Resume>().Query().FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId, ct), "CV", resumeId);

        var analysisQuery = uow.Repository<ResumeAnalysis>().Query().Where(a => a.ResumeId == resumeId && a.UserId == userId);
        var analysis = analysisId is { } aid
            ? await analysisQuery.FirstOrDefaultAsync(a => a.Id == aid, ct)
            : await analysisQuery.OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);

        var professionId = analysis?.ProfessionId ?? resume.TargetProfessionId;
        var profession = professionId is null ? null : (await catalog.GetAsync(ct)).FirstOrDefault(p => p.Id == professionId);

        var fullText = ResumeTextComposer.Compose(resume.Content) + "\n" + (resume.RawText ?? string.Empty);
        var result = improver.Improve(resume.Content, fullText, analysis, profession);
        var changes = result.Changes;
        var engine = "Kural motoru";

        if (ai.IsEnabled)
        {
            var aiChanges = await RewriteWithAiAsync(resume.Content, profession, analysis, ct);
            if (aiChanges.Count > 0)
            {
                // Yapay zekanın yeniden yazdığı alanlar kural motorunun aynı alandaki önerisinin yerini alır.
                var aiPaths = aiChanges.Select(c => c.Path).ToHashSet();
                changes = changes.Where(c => !aiPaths.Contains(c.Path)).Concat(aiChanges).ToList();
                engine = "Kural motoru + " + ai.Name;
            }
        }

        var ordered = changes
            .OrderBy(c => SectionOrder(c.Section))
            .Select((c, i) => c with { Id = $"c{i + 1}" })
            .ToList();

        return new ImprovementPreviewDto(resume.Id, resume.Title, analysis?.Id, analysis?.OverallScore, ordered, result.ManualTodos, engine);
    }

    public async Task<ImprovementApplyResultDto> ApplyAsync(Guid resumeId, ImprovementApplyRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        Guard.Against(request.Changes.Count == 0, "Uygulanacak en az bir değişiklik seçin.");

        var resume = Guard.Found(
            await uow.Repository<Resume>().Tracked().FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId, ct), "CV", resumeId);

        var content = JsonSerializer.Deserialize<ResumeContent>(JsonSerializer.Serialize(resume.Content)) ?? new ResumeContent();
        var applied = improver.Apply(content, request.Changes);
        Guard.Against(applied == 0, "Seçilen değişiklikler uygulanamadı. Sayfayı yenileyip tekrar deneyin.");

        Resume target;
        if (request.SaveAsCopy)
        {
            target = new Resume
            {
                UserId = userId,
                Title = (resume.Title + " (iyileştirilmiş)").Truncate(150),
                Template = resume.Template,
                Source = ResumeSource.Manual,
                TargetProfessionId = request.ProfessionId ?? resume.TargetProfessionId,
                Content = content
            };
            await uow.Repository<Resume>().AddAsync(target, ct);
        }
        else
        {
            resume.Content = content;
            resume.UpdatedAt = DateTime.UtcNow; // Düzenlenmiş CV artık ham dosya metniyle değil, içeriğiyle analiz edilir.
            target = resume;
        }

        await uow.SaveChangesAsync(ct);
        logger.LogInformation("{Count} iyileştirme uygulandı. CV: {ResumeId}", applied, target.Id);

        if (!request.Reanalyze)
        {
            return new ImprovementApplyResultDto(target.ToDto(), null, null, applied);
        }

        var analysis = await analyses.AnalyzeAsync(target.Id, new AnalyzeRequest(request.ProfessionId ?? target.TargetProfessionId, request.City), ct);
        return new ImprovementApplyResultDto(target.ToDto(), analysis.Id, analysis.OverallScore, applied);
    }

    private async Task<List<ResumeChangeDto>> RewriteWithAiAsync(ResumeContent c, CatalogProfession? profession, ResumeAnalysis? analysis, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            summary = c.Summary,
            title = c.Personal.Title,
            skills = c.Skills,
            experiences = c.Experiences.Select((e, i) => new { index = i, e.Position, e.Company, e.StartDate, e.EndDate, e.Description })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false });

        var reply = AiJson.TryParse<AiImproveResponse>(await ai.CompleteAsync(
            AiPrompts.System,
            AiImprovePrompt.Build(payload.Truncate(10000), profession?.Name, analysis?.Details.Improvements ?? new List<string>()),
            jsonOutput: true,
            ct));

        var list = new List<ResumeChangeDto>();
        if (reply is null)
        {
            return list;
        }

        if (reply.Summary.HasValue() && reply.Summary!.Trim() != (c.Summary ?? string.Empty).Trim())
        {
            list.Add(new ResumeChangeDto("", "Özet", "Profesyonel özet", "summary", c.Summary.HasValue() ? "modified" : "added", "text",
                c.Summary, reply.Summary.Trim(), "Yapay zeka özeti CV'nizdeki bilgilerle, kalıp ifadelerden arındırarak yeniden yazdı.", ai.Name));
        }

        foreach (var exp in reply.Experiences ?? new List<AiImprovedExperience>())
        {
            if (exp.Index < 0 || exp.Index >= c.Experiences.Count || exp.Bullets is not { Count: > 0 })
            {
                continue;
            }

            var e = c.Experiences[exp.Index];
            var after = string.Join("\n", exp.Bullets.Where(b => b.HasValue()).Select(b => "• " + b.Trim().TrimStart('•', '-', ' ')));
            if (after.Length > 0 && after != (e.Description ?? string.Empty).Trim())
            {
                var label = string.Join(" · ", new[] { e.Position, e.Company }.Where(x => x.HasValue()));
                list.Add(new ResumeChangeDto("", "İş deneyimi", $"{label} — açıklama", $"experience:{exp.Index}:description", "modified", "text",
                    e.Description, after, "Yapay zeka maddeleri eylem fiilleriyle, tek fikirli ve taranabilir biçimde yeniden yazdı. Sayı eklenmedi; varsa kendiniz ekleyin.", ai.Name));
            }
        }

        return list;
    }

    private static int SectionOrder(string section) => section switch
    {
        "Kişisel bilgiler" => 0,
        "Özet" => 1,
        "İş deneyimi" => 2,
        "Eğitim" => 3,
        "Beceriler" => 4,
        _ => 5
    };
}
