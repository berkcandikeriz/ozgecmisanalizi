using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

[Route("basvurular")]
public sealed class ApplicationsController(ApiClient api) : AppController(api)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new ApplicationsViewModel
    {
        Items = await Api.GetAsync<IReadOnlyList<JobApplicationDto>>("api/applications", ct),
        Resumes = await Api.GetAsync<IReadOnlyList<ResumeListItemDto>>("api/resumes", ct)
    });

    [HttpPost("ekle")]
    public async Task<IActionResult> Create(ApplicationsViewModel model, CancellationToken ct)
    {
        await Api.PostAsync<JobApplicationDto>("api/applications", model.NewItem, ct);
        Success("Başvuru eklendi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        ViewBag.Resumes = await Api.GetAsync<IReadOnlyList<ResumeListItemDto>>("api/resumes", ct);
        return View(await Api.GetAsync<JobApplicationDto>($"api/applications/{id}", ct));
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, JobApplicationSaveRequest request, CancellationToken ct)
    {
        await Api.PutAsync<JobApplicationDto>($"api/applications/{id}", request, ct);
        Success("Başvuru güncellendi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/asama")]
    public async Task<IActionResult> Stage(Guid id, ApplicationStage stage, CancellationToken ct)
    {
        await Api.PatchAsync<JobApplicationDto>($"api/applications/{id}/stage", new StageChangeRequest(stage), ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/sil")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Api.DeleteAsync($"api/applications/{id}", ct);
        Success("Başvuru silindi.");
        return RedirectToAction(nameof(Index));
    }
}
