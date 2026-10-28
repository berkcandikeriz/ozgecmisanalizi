using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

public sealed class DashboardController(ApiClient api) : AppController(api)
{
    [HttpGet("/panel")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await Api.GetAsync<DashboardDto>("api/dashboard", ct));
}
