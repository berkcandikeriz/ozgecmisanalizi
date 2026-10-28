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
    public async Task<IActionResult> Pdf(Guid id, ResumeTemplate? template, CancellationToken ct)
    {
        var url = $"api/resumes/{id}/pdf" + (template is null ? string.Empty : $"?template={template}");
        return PdfFile(await Api.GetFileAsync(url, ct));
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
