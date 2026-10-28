using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

public sealed class AnalysisController(ApiClient api) : AppController(api)
{
    [HttpGet("/analizler")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await Api.GetAsync<IReadOnlyList<AnalysisListItemDto>>("api/analyses", ct));

    [HttpGet("/analiz/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var analysis = await Api.GetAsync<AnalysisDto>($"api/analyses/{id}", ct);
        var professions = await Api.GetAsync<IReadOnlyList<ProfessionListItemDto>>("api/professions", ct);
        return View(new AnalysisViewModel { Analysis = analysis, Professions = professions });
    }

    [HttpGet("/analiz/{id:guid}/rapor")]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct) => PdfFile(await Api.GetFileAsync($"api/analyses/{id}/report", ct));

    [HttpPost("/analiz/{id:guid}/sil")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Api.DeleteAsync($"api/analyses/{id}", ct);
        Success("Analiz silindi.");
        return RedirectToAction(nameof(Index));
    }
}
