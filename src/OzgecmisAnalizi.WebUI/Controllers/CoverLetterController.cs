using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

[Route("on-yazi")]
public sealed class CoverLetterController(ApiClient api) : AppController(api)
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? jobTitle, string? company, Guid? applicationId, CancellationToken ct)
    {
        var resumes = await Api.GetAsync<IReadOnlyList<ResumeListItemDto>>("api/resumes", ct);
        return View(new CoverLetterIndexViewModel
        {
            Letters = await Api.GetAsync<IReadOnlyList<CoverLetterDto>>("api/cover-letters", ct),
            Resumes = resumes,
            Form = new CoverLetterFormModel
            {
                ResumeId = resumes.FirstOrDefault()?.Id ?? Guid.Empty,
                JobTitle = jobTitle ?? string.Empty,
                Company = company ?? string.Empty,
                JobApplicationId = applicationId
            }
        });
    }

    [HttpPost("olustur")]
    public async Task<IActionResult> Generate([Bind(Prefix = "Form")] CoverLetterFormModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid || form.ResumeId == Guid.Empty)
        {
            Error("CV, pozisyon ve şirket alanlarını doldurun.");
            return RedirectToAction(nameof(Index));
        }

        var letter = await Api.PostAsync<CoverLetterDto>("api/cover-letters",
            new CoverLetterRequest(form.ResumeId, form.JobTitle, form.Company, form.JobDescription, form.Tone, form.JobApplicationId), ct);
        Success("Ön yazınız hazır. Göndermeden önce okuyup kişiselleştirin.");
        return RedirectToAction(nameof(Details), new { id = letter.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await Api.GetAsync<CoverLetterDto>($"api/cover-letters/{id}", ct));

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, string content, CancellationToken ct)
    {
        await Api.PutAsync<CoverLetterDto>($"api/cover-letters/{id}", new CoverLetterUpdateRequest(content), ct);
        Success("Ön yazı kaydedildi.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/sil")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Api.DeleteAsync($"api/cover-letters/{id}", ct);
        Success("Ön yazı silindi.");
        return RedirectToAction(nameof(Index));
    }
}
