using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>Ön yazı (cover letter) üretimi ve düzenleme.</summary>
[Route("api/cover-letters")]
public sealed class CoverLettersController(ICoverLetterService letters) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CoverLetterDto>>>> List(CancellationToken ct) =>
        OkResponse(await letters.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CoverLetterDto>>> Get(Guid id, CancellationToken ct) =>
        OkResponse(await letters.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CoverLetterDto>>> Generate(CoverLetterRequest request, CancellationToken ct) =>
        OkResponse(await letters.GenerateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CoverLetterDto>>> Update(Guid id, CoverLetterUpdateRequest request, CancellationToken ct) =>
        OkResponse(await letters.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await letters.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Ön yazı silindi."));
    }
}
