using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Ai;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Business.Pdf;
using OzgecmisAnalizi.Business.Translation;
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
    IAiProvider ai,
    IResumeTranslator translator) : IResumeService
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
        var extracted = await extractor.ExtractAsync(file, fileName, ct);
        var text = extracted.Text;
        var content = parser.Parse(text, extracted.Links);

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

    public async Task<(byte[] Content, string FileName)> RenderPdfAsync(Guid id, ResumeTemplate? template, string? language = null, bool includePhoto = true, CancellationToken ct = default)
    {
        Guard.Against(language.HasValue() && !CvLanguages.IsSupported(language), $"Desteklenmeyen dil: {language}.");
        var resume = await LoadOwnedAsync(id, false, ct);
        var own = CvLanguages.Normalize(resume.Content.Language);
        var lang = language.HasValue() ? CvLanguages.Normalize(language) : own;

        // Dil istenmediyse ya da CV zaten o dildeyse içerik olduğu gibi basılır; yalnızca başlıklar CV'nin diline göre seçilir.
        var content = lang == own ? resume.Content : (await translator.TranslateAsync(resume.Content, lang, ct)).Content;
        var bytes = pdf.Render(content, template ?? resume.Template, lang, includePhoto);
        var name = (resume.Content.Personal.FullName ?? resume.Title).ToSlug();
        var suffix = lang == own ? "cv" : $"cv-{lang}";
        return (bytes, $"{(name.Length > 0 ? name : "cv")}-{suffix}.pdf");
    }

    public TranslationOptionsDto GetTranslationOptions() =>
        new(CvLanguages.List(), translator.EngineName, translator.FullTranslationAvailable);

    public async Task<ResumeTranslationResultDto> TranslateAsync(Guid id, ResumeTranslateRequest request, CancellationToken ct = default)
    {
        Guard.Against(!CvLanguages.IsSupported(request.Language), $"Desteklenmeyen dil: {request.Language}.");
        var lang = CvLanguages.Normalize(request.Language);
        var source = await LoadOwnedAsync(id, false, ct);
        Guard.Against(lang == CvLanguages.Normalize(source.Content.Language), "CV zaten bu dilde; farklı bir hedef dil seçin.");
        var outcome = await translator.TranslateAsync(source.Content, lang, ct);

        string? note = null;
        if (!outcome.Complete)
        {
            note = translator.FullTranslationAvailable
                ? $"{outcome.TotalCount - outcome.TranslatedCount} metin parçası çevrilemedi (çeviri servisine ulaşılamamış olabilir). Kopyada bu alanları elle düzeltebilir ya da yeniden deneyebilirsiniz."
                : "Yapay zeka veya LibreTranslate tanımlı olmadığı için yalnızca başlıklar, dil seviyeleri ve sözlükteki unvan/beceri/bölüm adları çevrildi. " +
                  "Özet ve deneyim maddeleri için appsettings'te Ai (Gemini/Ollama) ya da Translation:LibreTranslateUrl tanımlayın veya kopyada elle çevirin.";
        }

        ResumeDto? copyDto = null;
        if (request.SaveAsCopy)
        {
            var copy = new Resume
            {
                UserId = source.UserId,
                Title = $"{source.Title} ({CvLanguages.DisplayName(lang)})".Truncate(150),
                Template = source.Template,
                Source = ResumeSource.Manual,
                TargetProfessionId = source.TargetProfessionId,
                Content = Sanitize(outcome.Content)
            };
            if (!request.IncludePhoto)
            {
                copy.Content.Personal.Photo = null;
            }

            await Resumes.AddAsync(copy, ct);
            await uow.SaveChangesAsync(ct);
            copyDto = copy.ToDto();
        }

        return new ResumeTranslationResultDto(lang, outcome.Engine, outcome.Complete, outcome.TranslatedCount, outcome.TotalCount, note, copyDto);
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

    public async Task<TailorApplyResultDto> ApplyTailoringAsync(Guid id, TailorApplyRequest request, CancellationToken ct = default)
    {
        var jobTitle = Guard.Required(request.JobTitle, "Pozisyon", 200);
        var source = await LoadOwnedAsync(id, false, ct);
        var content = Clone(source.Content);

        // Kullanıcının "bende var" diye onayladığı anahtar kelimeler öne eklenir; ATS ilk becerilere daha çok ağırlık verir.
        content.Skills = request.AddKeywords
            .Where(k => k.HasValue())
            .Select(k => k.Trim().Truncate(60))
            .Concat(content.Skills)
            .DistinctBySearchKey()
            .Take(80)
            .ToList();

        if (request.Summary.HasValue())
        {
            content.Summary = request.Summary!.Trim().Truncate(3000);
        }

        if (request.UpdateTitle)
        {
            content.Personal.Title = jobTitle;
        }

        var label = request.Company.HasValue() ? $"{jobTitle} — {request.Company!.Trim()}" : jobTitle;
        var copy = new Resume
        {
            UserId = source.UserId,
            Title = label.Truncate(150),
            Template = source.Template,
            Source = ResumeSource.Manual,
            TargetProfessionId = source.TargetProfessionId,
            Content = content
        };
        await Resumes.AddAsync(copy, ct);

        Guid? applicationId = null;
        if (request.CreateApplication)
        {
            var application = new JobApplication
            {
                UserId = source.UserId,
                ResumeId = copy.Id,
                JobTitle = jobTitle,
                Company = request.Company.HasValue() ? request.Company!.Trim().Truncate(200) : "Belirtilmedi",
                Url = request.JobUrl.HasValue() && Uri.TryCreate(request.JobUrl, UriKind.Absolute, out _) ? request.JobUrl!.Trim().Truncate(1000) : null,
                Description = request.JobDescription?.Trim().Truncate(20000),
                Stage = ApplicationStage.Hazirlik,
                Source = "İlana göre uyarlama"
            };
            await uow.Repository<JobApplication>().AddAsync(application, ct);
            applicationId = application.Id;
        }

        await uow.SaveChangesAsync(ct);
        return new TailorApplyResultDto(copy.ToDto(), applicationId);
    }

    public async Task<ResumeChatResponse> ChatAsync(Guid id, ResumeChatRequest request, CancellationToken ct = default)
    {
        var messages = (request.Messages ?? Array.Empty<ChatMessageDto>()).Where(m => m.Content.HasValue()).TakeLast(12).ToList();
        Guard.Against(messages.Count == 0 || messages[^1].Role != "user", "Bir soru yazın.");
        var resume = await LoadOwnedAsync(id, false, ct);
        var lastAnalysis = await uow.Repository<ResumeAnalysis>().Query()
            .Where(a => a.ResumeId == id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (!ai.IsEnabled)
        {
            return new ResumeChatResponse(OfflineAdvice(messages[^1].Content, lastAnalysis), "Kural motoru");
        }

        var context = ResumeTextComposer.Compose(resume.Content).Truncate(9000);
        var analysisNote = lastAnalysis is null
            ? "Henüz analiz yapılmadı."
            : $"Son analiz puanı {lastAnalysis.OverallScore}/100 ({lastAnalysis.Label}). Öncelikli iyileştirmeler: {string.Join(" | ", lastAnalysis.Details.Improvements.Take(6))}";
        var transcript = string.Join("\n", messages.Select(m => $"{(m.Role == "user" ? "Kullanıcı" : "Danışman")}: {m.Content.Truncate(2000)}"));

        var prompt = $"""
            Sen kullanıcının CV danışmanısın. Yalnızca aşağıdaki CV'ye ve analize dayanarak, kısa ve uygulanabilir yanıt ver.
            Gerekirse CV'ye doğrudan yapıştırılabilecek örnek madde veya özet yaz; CV'de olmayan deneyim uydurma.

            ANALİZ: {analysisNote}

            CV:
            ---
            {context}
            ---

            KONUŞMA:
            {transcript}

            Danışman:
            """;

        var reply = await ai.CompleteAsync(AiPrompts.System, prompt, jsonOutput: false, ct);
        return reply.HasValue()
            ? new ResumeChatResponse(reply!.Trim(), ai.Name)
            : new ResumeChatResponse(OfflineAdvice(messages[^1].Content, lastAnalysis) + "\n\n(Yapay zeka sağlayıcısına şu anda ulaşılamadı.)", "Kural motoru");
    }

    /// <summary>Yapay zeka yokken, sorudaki anahtar kelimelere göre son analizden ilgili önerileri derler.</summary>
    private static string OfflineAdvice(string question, ResumeAnalysis? analysis)
    {
        if (analysis is null)
        {
            return "Önce bu CV'yi analiz edin; ardından hangi bölümü nasıl iyileştireceğinizi buradan sorabilirsiniz. " +
                   "Daha serbest bir sohbet için appsettings içinde ücretsiz bir yapay zeka sağlayıcısı (Gemini, Groq veya yerel Ollama) tanımlayabilirsiniz.";
        }

        var key = question.ToSearchKey();
        var d = analysis.Details;
        var topics = new (string[] Words, Func<IEnumerable<string>> Answer)[]
        {
            (new[] { "ozet", "summary", "profil" }, () => d.Sections.Where(s => s.Section == "Özet").SelectMany(s => s.Tips)),
            (new[] { "deneyim", "madde", "is ", "experience" }, () => d.Sections.Where(s => s.Section is "İş deneyimi" or "Dil ve anlatım").SelectMany(s => s.Tips)),
            (new[] { "beceri", "skill", "eksik", "ogren" }, () => d.MissingSkills.Take(6).Select(m => $"{m.Skill} ({m.Importance}){(m.HowToLearn is null ? "" : ": " + m.HowToLearn)}")),
            (new[] { "ats", "okun", "format", "tasarim" }, () => d.AtsChecks.Where(a => !a.Passed).Select(a => $"{a.Name}: {a.Detail}")),
            (new[] { "maas", "ucret", "salary" }, () => d.Salary is null ? Enumerable.Empty<string>() : new[] { $"{d.Salary.Level}: {d.Salary.MinNet:N0} – {d.Salary.MaxNet:N0} TL net. {d.Salary.Basis}" }),
            (new[] { "kariyer", "terfi", "sonraki" }, () => d.CareerPath.Where(c => !c.IsCurrent).Take(2).Select(c => $"{c.Title}: {string.Join(", ", c.Focus)}"))
        };

        var hits = topics.Where(t => t.Words.Any(w => key.Contains(w))).SelectMany(t => t.Answer()).Distinct().Take(6).ToList();
        if (hits.Count == 0)
        {
            hits = d.Improvements.Take(5).ToList();
        }

        return hits.Count == 0
            ? $"Son analizde puanınız {analysis.OverallScore}/100 ({analysis.Label}); kritik bir eksik görünmüyor. İlana göre uyarlama yaparak CV'nizi belirli bir pozisyona odaklayabilirsiniz."
            : "Son analizinize göre öneriler:\n• " + string.Join("\n• ", hits);
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
        c.Language = c.Language.HasValue() && CvLanguages.IsSupported(c.Language) && CvLanguages.Normalize(c.Language) != CvLanguages.Default
            ? CvLanguages.Normalize(c.Language)
            : null;
        c.Personal.Photo = ResumePhoto.Normalize(c.Personal.Photo);
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
