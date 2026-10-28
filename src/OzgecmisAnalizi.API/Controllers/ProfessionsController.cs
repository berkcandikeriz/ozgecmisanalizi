using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>Meslek kataloğu: beceriler, kariyer basamakları ve maaş aralıkları.</summary>
[Route("api/professions")]
[AllowAnonymous]
public sealed class ProfessionsController(IProfessionService professions) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProfessionListItemDto>>>> List([FromQuery] string? search, [FromQuery] string? category, CancellationToken ct) =>
        OkResponse(await professions.ListAsync(search, category, ct));

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> Categories(CancellationToken ct) =>
        OkResponse(await professions.CategoriesAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProfessionDto>>> Get(Guid id, CancellationToken ct) =>
        OkResponse(await professions.GetAsync(id, ct));
}
