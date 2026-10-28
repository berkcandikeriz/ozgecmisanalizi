using Microsoft.EntityFrameworkCore;
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
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Services;

public sealed class ResumeService(
    IUnitOfWork uow,
    ICurrentUser currentUser,
    IResumeTextExtractor extractor,
    IResumeTextParser parser,
    IResumePdfRenderer pdf,
    ISkillCatalog catalog,
    IAiProvider ai) : IResumeService
{
    private IRepository<Resume> Resumes => uow.Repository<Resume>();

    public async Task<IReadOnlyList<ResumeListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        return await Resumes.Query()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Select(r => new ResumeListItemDto(
                r.Id,
                r.Title,
                r.Template,
                r.Source,
                r.TargetProfession != null ? r.TargetProfession.Name : null,
                r.Analyses.Where(a => !a.IsDeleted).OrderByDescending(a => a.CreatedAt).Select(a => (int?)a.OverallScore).FirstOrDefault(),
                r.Analyses.Where(a => !a.IsDeleted).OrderByDescending(a => a.CreatedAt).Select(a => (Guid?)a.Id).FirstOrDefault(),
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<ResumeDto> GetAsync(Guid id, CancellationToken ct = default) => (await LoadOwnedAsync(id, false, ct)).ToDto();

    public async Task<ResumeDto> CreateAsync(ResumeSaveRequest request, CancellationToken ct = default)
    {
        var resume = new Resume { UserId = currentUser.RequireId(), Source = ResumeSource.Manual };
        Apply(resume, request);
        await Resumes.AddAsync(resume, ct);
        await uow.SaveChangesAsync(ct);
        return resume.ToDto();
    }

    public async Task<ResumeDto> UpdateAsync(Guid id, ResumeSaveRequest request, CancellationToken ct = default)
    {
        var resume = await LoadOwnedAsync(id, true, ct);
        Apply(resume, request);
        await uow.SaveChangesAsync(ct);
        return resume.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resume = await LoadOwnedAsync(id, true, ct);
        Resumes.Remove(resume);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<ResumeDto> UploadAsync(Stream file, string fileName, string? title, CancellationToken ct = default)
    {
        var text = await extractor.ExtractAsync(file, fileName, ct);
        var content = parser.Parse(text);

        // Beceri bölümü bulunamadıysa katalogdan metinde geçen becerileri öner.
        if (content.Skills.Count == 0)
        {
            var prepared = SkillMatcher.PrepareText(text);
            var skills = await catalog.AllSkillsAsync(ct);
            content.Skills = skills.Where(s => SkillMatcher.ContainsAny(prepared, s.Keys)).Select(s => s.Name).Take(25).ToList();
        }

        var resume = new Resume
        {
            UserId = currentUser.RequireId(),
            Title = title.HasValue() ? title!.Trim().Truncate(150) : Path.GetFileNameWithoutExtension(fileName).Truncate(150),
            Source = ResumeSource.Upload,
            Template = ResumeTemplate.Modern,
            OriginalFileName = Path.GetFileName(fileName).Truncate(260),
            RawText = text,
            Content = content
        };

        await Resumes.AddAsync(resume, ct);
        await uow.SaveChangesAsync(ct);
        return resume.ToDto();
    }

    public async Task<ResumeDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var source = await LoadOwnedAsync(id, false, ct);
        var copy = new Resume
        {
            UserId = source.UserId,
            Title = (source.Title + " (kopya)").Truncate(150),
            Template = source.Template,
            Source = ResumeSource.Manual,
            TargetProfessionId = source.TargetProfessionId,
            Content = Clone(source.Content)
        };
        await Resumes.AddAsync(copy, ct);
        await uow.SaveChangesAsync(ct);
        return copy.ToDto();
    }

    public async Task<(byte[] Content, string FileName)> RenderPdfAsync(Guid id, ResumeTemplate? template, CancellationToken ct = default)
    {
        var resume = await LoadOwnedAsync(id, false, ct);
        var bytes = pdf.Render(resume.Content, template ?? resume.Template);
        var name = (resume.Content.Personal.FullName ?? resume.Title).ToSlug();
        return (bytes, $"{(name.Length > 0 ? name : "cv")}-cv.pdf");
    }

    public async Task<TailorResultDto> TailorAsync(Guid id, TailorRequest request, CancellationToken ct = default)
    {
        var jd = Guard.Required(request.JobDescription, "İlan metni", 20000);
        Guard.Against(jd.WordCount() < 15, "İlan metni çok kısa; ilanın tamamını yapıştırın.");

        var resume = await LoadOwnedAsync(id, false, ct);
        var resumeText = ResumeTextComposer.Compose(resume.Content) + "\n" + (resume.RawText ?? string.Empty);
        var preparedResume = SkillMatcher.PrepareText(resumeText);
        var preparedJd = SkillMatcher.PrepareText(jd + " " + request.JobTitle);

        var skills = await catalog.AllSkillsAsync(ct);
        var required = skills.Where(s => SkillMatcher.ContainsAny(preparedJd, s.Keys)).ToList();
        var matched = required.Where(s => SkillMatcher.ContainsAny(preparedResume, s.Keys)).Select(s => s.Name).Distinct().ToList();
        var missing = required.Where(s => !SkillMatcher.ContainsAny(preparedResume, s.Keys)).Select(s => s.Name).Distinct().ToList();
        var percent = required.Count == 0 ? 0 : (int)Math.Round(matched.Count * 100.0 / (matched.Count + missing.Count));

        var suggestions = new List<string>();
        if (missing.Count > 0)
        {
            suggestions.Add($"İlanda geçen ama CV'nizde olmayan anahtar kelimeler: {string.Join(", ", missing.Take(10))}. Bu becerilere sahipseniz beceriler bölümüne ve ilgili deneyim maddelerine ekleyin.");
        }

        if (request.JobTitle.HasValue() && !preparedResume.Contains(SkillMatcher.NormalizeKey(request.JobTitle!), StringComparison.Ordinal))
        {
            suggestions.Add($"CV başlığınızı ilandaki unvanla uyumlu hale getirin: \"{request.JobTitle}\".");
        }

        suggestions.Add("Özetinizin ilk cümlesinde ilandaki en önemli 2-3 gereksinimi karşıladığınızı gösterin.");
        suggestions.Add("İlandaki sorumluluklara karşılık gelen deneyim maddelerini en üste taşıyın ve sonuçlarını sayılarla yazın.");

        string? summary = null;
        var engine = "Kural motoru";
        if (ai.IsEnabled)
        {
            var reply = AiJson.TryParse<AiTailorResponse>(
                await ai.CompleteAsync(AiPrompts.System, AiPrompts.Tailor(resumeText.Truncate(9000), jd.Truncate(6000), missing), jsonOutput: true, ct));
            if (reply is not null)
            {
                summary = reply.Summary;
                if (reply.Suggestions is { Count: > 0 })
                {
                    suggestions.InsertRange(0, reply.Suggestions.Take(6));
                }

                engine = "Kural motoru + " + ai.Name;
            }
        }

        return new TailorResultDto(percent, matched, missing, suggestions.Distinct().Take(10).ToList(), summary, engine);
    }

    private async Task<Resume> LoadOwnedAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var userId = currentUser.RequireId();
        var query = tracked ? Resumes.Tracked() : Resumes.Query();
        var resume = await query.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        return Guard.Found(resume, "CV", id);
    }

    private static void Apply(Resume resume, ResumeSaveRequest request)
    {
        resume.Title = Guard.Required(request.Title, "CV başlığı", 150);
        resume.Template = request.Template;
        resume.TargetProfessionId = request.TargetProfessionId;
        resume.Content = Sanitize(request.Content ?? new ResumeContent());
    }

    /// <summary>Boş satırları ve fazlalıkları temizler, alan uzunluklarını sınırlar.</summary>
    private static ResumeContent Sanitize(ResumeContent c)
    {
        c.Summary = c.Summary?.Trim().Truncate(3000);
        c.Experiences = c.Experiences.Where(e => e.Position.HasValue() || e.Company.HasValue()).Take(30).ToList();
        foreach (var e in c.Experiences)
        {
            e.Description = e.Description?.Trim().Truncate(4000);
            if (e.IsCurrent)
            {
                e.EndDate = null;
            }
        }

        c.Educations = c.Educations.Where(e => e.School.HasValue() || e.Degree.HasValue()).Take(15).ToList();
        c.Skills = c.Skills.Where(s => s.HasValue()).Select(s => s.Trim().Truncate(60)).DistinctBySearchKey().Take(80).ToList();
        c.Languages = c.Languages.Where(l => l.Name.HasValue()).Take(15).ToList();
        c.Certificates = c.Certificates.Where(x => x.Name.HasValue()).Take(40).ToList();
        c.Projects = c.Projects.Where(x => x.Name.HasValue()).Take(20).ToList();
        return c;
    }

    private static ResumeContent Clone(ResumeContent content) =>
        System.Text.Json.JsonSerializer.Deserialize<ResumeContent>(System.Text.Json.JsonSerializer.Serialize(content)) ?? new ResumeContent();
}
