using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

[AllowAnonymous]
[Route("meslekler")]
public sealed class ProfessionsController(ApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? category, CancellationToken ct)
    {
        var qs = QueryString.Create(new Dictionary<string, string?> { ["search"] = search, ["category"] = category });
        return View(new ProfessionsViewModel
        {
            Items = await api.GetAsync<IReadOnlyList<ProfessionListItemDto>>("api/professions" + qs, ct),
            Categories = await api.GetAsync<IReadOnlyList<string>>("api/professions/categories", ct),
            Search = search,
            Category = category
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await api.GetAsync<ProfessionDto>($"api/professions/{id}", ct));
}
