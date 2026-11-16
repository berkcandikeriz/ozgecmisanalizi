using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

[Route("cv")]
public sealed class ResumeController(ApiClient api) : AppController(api)
{
    private const long MaxBytes = 10 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await Api.GetAsync<IReadOnlyList<ResumeListItemDto>>("api/resumes", ct));

    [HttpGet("yukle")]
    public IActionResult Upload() => View(new UploadViewModel());

    [HttpPost("yukle")]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(UploadViewModel model, IFormFile? file, bool analyze, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Bir CV dosyası seçin (PDF, DOCX veya TXT).");
            return View(model);
        }

        if (file.Length > MaxBytes)
        {
            ModelState.AddModelError(string.Empty, "Dosya 10 MB sınırını aşıyor.");
            return View(model);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var resume = await Api.UploadAsync<ResumeDto>("api/resumes/upload", stream, file.FileName, file.ContentType,
                new Dictionary<string, string?> { ["title"] = model.Title }, ct);

            if (analyze)
            {
                var analysis = await Api.PostAsync<AnalysisDto>($"api/analyses/resume/{resume.Id}", new AnalyzeRequest(null, User.FindFirst("city")?.Value), ct);
                return RedirectToAction("Details", "Analysis", new { id = analysis.Id });
            }

            Success("CV'niz yüklendi. Ayrıştırılan bilgileri kontrol edin.");
            return RedirectToAction(nameof(Edit), new { id = resume.Id });
        }
        catch (ApiException ex) when (ex.StatusCode is not System.Net.HttpStatusCode.Unauthorized)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost("yeni")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var content = new ResumeContent
        {
            Personal = new PersonalInfo { FullName = User.Identity?.Name, Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value, City = User.FindFirst("city")?.Value }
        };
        var dto = await Api.PostAsync<ResumeDto>("api/resumes", new ResumeSaveRequest { Title = "Yeni CV", Content = content }, ct);
        return RedirectToAction(nameof(Edit), new { id = dto.Id });
    }

    [HttpGet("{id:guid}/duzenle")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var resume = await Api.GetAsync<ResumeDto>($"api/resumes/{id}", ct);
        var vm = await BuildEditModelAsync(id, new ResumeSaveRequest
        {
            Title = resume.Title,
            Template = resume.Template,
            TargetProfessionId = resume.TargetProfessionId,
            Content = resume.Content
        }, ct);
        return View(vm);
    }

    [HttpPost("{id:guid}/duzenle")]
    public async Task<IActionResult> Edit(Guid id, ResumeEditViewModel model, string? next, CancellationToken ct)
    {
        model.Resume.Content ??= new ResumeContent();
        model.Resume.Content.Skills = (model.SkillsText ?? string.Empty)
            .Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        try
        {
            await Api.PutAsync<ResumeDto>($"api/resumes/{id}", model.Resume, ct);
        }
        catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.UnprocessableEntity)
        {
            ModelState.AddModelError(string.Empty, ex.Message + " " + string.Join(" ", ex.Errors));
            return View(await BuildEditModelAsync(id, model.Resume, ct));
        }

        switch (next)
        {
            case "analyze":
                var analysis = await Api.PostAsync<AnalysisDto>($"api/analyses/resume/{id}", new AnalyzeRequest(model.Resume.TargetProfessionId, User.FindFirst("city")?.Value), ct);
                return RedirectToAction("Details", "Analysis", new { id = analysis.Id });
            case "pdf":
                return RedirectToAction(nameof(Pdf), new { id, template = model.Resume.Template });
            default:
                Success("CV kaydedildi.");
                return RedirectToAction(nameof(Edit), new { id });
        }
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, ResumeTemplate? template, [FromQuery(Name = "dil")] string? language, [FromQuery(Name = "foto")] bool photo = true, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (template is not null) query.Add($"template={template}");
        if (!photo) query.Add("photo=false");
        if (!string.IsNullOrWhiteSpace(language)) query.Add($"language={Uri.EscapeDataString(language)}");
        var url = $"api/resumes/{id}/pdf" + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
        return PdfFile(await Api.GetFileAsync(url, ct));
    }

    /// <summary>CV'yi yabancı dile çevirme ekranı (örn. İngilizce PDF).</summary>
    [HttpGet("{id:guid}/cevir")]
    public async Task<IActionResult> Translate(Guid id, [FromQuery(Name = "dil")] string? language, [FromQuery(Name = "analiz")] Guid? analysisId, CancellationToken ct)
    {
        var resume = await Api.GetAsync<ResumeDto>($"api/resumes/{id}", ct);
        var options = await Api.GetAsync<TranslationOptionsDto>("api/resumes/translation/languages", ct);
        var current = resume.Content.Language ?? "tr";
        var target = options.Languages.Any(l => l.Code == language && l.Code != current)
            ? language!
            : options.Languages.Select(l => l.Code).FirstOrDefault(c => c == "en" && c != current) ?? options.Languages.First(l => l.Code != current).Code;
        return View(new TranslateViewModel { Resume = resume, Options = options, Language = target, Template = resume.Template, AnalysisId = analysisId, IncludePhoto = target != "en" });
    }

    [HttpPost("{id:guid}/cevir")]
    public async Task<IActionResult> Translate(Guid id, TranslateFormModel form, CancellationToken ct)
    {
        if (form.Output == "pdf")
        {
            return RedirectToAction(nameof(Pdf), new { id, template = form.Template, dil = form.Language, foto = form.IncludePhoto });
        }

        try
        {
            var result = await Api.PostAsync<ResumeTranslationResultDto>($"api/resumes/{id}/translate",
                new ResumeTranslateRequest { Language = form.Language, SaveAsCopy = true, IncludePhoto = form.IncludePhoto }, ct);
            if (result.Note is not null)
            {
                Info(result.Note);
            }
            else
            {
                Success($"CV çevrildi ({result.Engine}). Çeviriyi kontrol edip PDF olarak indirebilirsiniz.");
            }

            return RedirectToAction(nameof(Edit), new { id = result.Copy!.Id });
        }
        catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.UnprocessableEntity)
        {
            Error(string.Join(" ", ex.Errors.DefaultIfEmpty(ex.Message)));
            return RedirectToAction(nameof(Translate), new { id, dil = form.Language });
        }
    }

    [HttpPost("{id:guid}/analiz-et")]
    public async Task<IActionResult> Analyze(Guid id, Guid? professionId, CancellationToken ct)
    {
        var analysis = await Api.PostAsync<AnalysisDto>($"api/analyses/resume/{id}", new AnalyzeRequest(professionId, User.FindFirst("city")?.Value), ct);
        return RedirectToAction("Details", "Analysis", new { id = analysis.Id });
    }

    [HttpPost("{id:guid}/kopyala")]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var copy = await Api.PostAsync<ResumeDto>($"api/resumes/{id}/duplicate", null, ct);
        Success("CV'nin bir kopyası oluşturuldu. İlana özel düzenleyebilirsiniz.");
        return RedirectToAction(nameof(Edit), new { id = copy.Id });
    }

    [HttpPost("{id:guid}/sil")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Api.DeleteAsync($"api/resumes/{id}", ct);
        Success("CV silindi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/uyarla")]
    public async Task<IActionResult> Tailor(Guid id, CancellationToken ct) =>
        View(new TailorViewModel { Resume = await Api.GetAsync<ResumeDto>($"api/resumes/{id}", ct) });

    [HttpPost("{id:guid}/uyarla")]
    public async Task<IActionResult> Tailor(Guid id, TailorViewModel model, CancellationToken ct)
    {
        model.Resume = await Api.GetAsync<ResumeDto>($"api/resumes/{id}", ct);
        if (string.IsNullOrWhiteSpace(model.JobDescription))
        {
            ModelState.AddModelError(nameof(model.JobDescription), "İlan metnini yapıştırın.");
            return View(model);
        }

        try
        {
            model.Result = await Api.PostAsync<TailorResultDto>($"api/resumes/{id}/tailor", new TailorRequest(model.JobDescription, model.JobTitle), ct);
        }
        catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.UnprocessableEntity)
        {
            ModelState.AddModelError(string.Empty, string.Join(" ", ex.Errors.DefaultIfEmpty(ex.Message)));
        }

        return View(model);
    }

    [HttpPost("{id:guid}/uyarla/uygula")]
    public async Task<IActionResult> ApplyTailoring(Guid id, TailorApplyRequest request, CancellationToken ct)
    {
        var result = await Api.PostAsync<TailorApplyResultDto>($"api/resumes/{id}/tailor/apply", request, ct);
        Success(result.ApplicationId is null
            ? "Uyarlanmış CV kopyası oluşturuldu. Kontrol edip PDF olarak indirebilirsiniz."
            : "Uyarlanmış CV oluşturuldu ve başvuru panonuza \"Hazırlık\" aşamasında eklendi.");
        return RedirectToAction(nameof(Edit), new { id = result.Resume.Id });
    }

    /// <summary>"Önerileri uygula": öneriler CV'ye uygulanmış hâliyle, önce/sonra karşılaştırmalı gösterilir.</summary>
    [HttpGet("{id:guid}/iyilestir")]
    public async Task<IActionResult> Improve(Guid id, [FromQuery(Name = "analiz")] Guid? analysisId, CancellationToken ct)
    {
        var url = $"api/resumes/{id}/improve" + (analysisId is null ? string.Empty : $"?analysisId={analysisId}");
        return View(await Api.PostAsync<ImprovementPreviewDto>(url, null, ct));
    }

    [HttpPost("{id:guid}/iyilestir")]
    public async Task<IActionResult> Improve(Guid id, ImproveFormModel form, CancellationToken ct)
    {
        var selected = form.Selected.Distinct().Where(i => i >= 0 && i < form.Changes.Count).Select(i => form.Changes[i]).ToList();
        if (selected.Count == 0)
        {
            Error("Uygulamak için en az bir değişiklik seçin.");
            return RedirectToAction(nameof(Improve), new { id });
        }

        var result = await Api.PostAsync<ImprovementApplyResultDto>($"api/resumes/{id}/improve/apply", new ImprovementApplyRequest
        {
            Changes = selected,
            SaveAsCopy = form.SaveAsCopy,
            Reanalyze = form.Reanalyze,
            ProfessionId = form.ProfessionId,
            City = User.FindFirst("city")?.Value
        }, ct);

        if (result.AnalysisId is { } newAnalysis)
        {
            var delta = form.PreviousScore is int before && result.NewScore is int after ? $" Puan: {before} → {after}." : string.Empty;
            Success($"{result.AppliedCount} değişiklik uygulandı.{delta}");
            return Redirect($"/analiz/{newAnalysis}" + (form.PreviousScore is int p ? $"?onceki={p}" : string.Empty));
        }

        Success($"{result.AppliedCount} değişiklik uygulandı. Kalan düzenlemeleri burada yapabilirsiniz.");
        return RedirectToAction(nameof(Edit), new { id = result.Resume.Id });
    }

    [HttpGet("{id:guid}/danisman")]
    public async Task<IActionResult> Chat(Guid id, CancellationToken ct)
    {
        var resume = await Api.GetAsync<ResumeDto>($"api/resumes/{id}", ct);
        var analyses = await Api.GetAsync<IReadOnlyList<AnalysisListItemDto>>($"api/analyses?resumeId={id}", ct);
        return View(new ChatViewModel { Resume = resume, LastAnalysis = analyses.FirstOrDefault() });
    }

    /// <summary>Sayfadaki sohbet kutusu bu uç noktayı fetch ile çağırır; geçmiş tarayıcıda tutulur.</summary>
    [HttpPost("{id:guid}/danisman")]
    public async Task<IActionResult> ChatSend(Guid id, [FromBody] ResumeChatRequest request, CancellationToken ct)
    {
        try
        {
            return Json(await Api.PostAsync<ResumeChatResponse>($"api/resumes/{id}/chat", request, ct));
        }
        catch (ApiException ex) when (ex.StatusCode is not System.Net.HttpStatusCode.Unauthorized)
        {
            return Json(new ResumeChatResponse(ex.Errors.FirstOrDefault() ?? ex.Message, "Hata"));
        }
    }

    private async Task<ResumeEditViewModel> BuildEditModelAsync(Guid id, ResumeSaveRequest request, CancellationToken ct)
    {
        var professions = await Api.GetAsync<IReadOnlyList<ProfessionListItemDto>>("api/professions", ct);
        var analyses = await Api.GetAsync<IReadOnlyList<AnalysisListItemDto>>($"api/analyses?resumeId={id}", ct);
        var last = analyses.FirstOrDefault();
        return new ResumeEditViewModel
        {
            Id = id,
            Resume = request,
            SkillsText = string.Join(", ", request.Content.Skills),
            Professions = professions,
            LastScore = last?.OverallScore,
            LastAnalysisId = last?.Id
        };
    }
}
