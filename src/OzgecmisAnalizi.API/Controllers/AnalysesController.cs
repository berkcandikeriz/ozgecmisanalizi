using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>CV analizi: puanlar, ATS kontrolleri, eksik beceriler, maaş tahmini, kariyer yolu ve PDF rapor.</summary>
[Route("api/analyses")]
public sealed class AnalysesController(IAnalysisService analyses) : ApiControllerBase
{
    [HttpPost("resume/{resumeId:guid}")]
    public async Task<ActionResult<ApiResponse<AnalysisDto>>> Analyze(Guid resumeId, AnalyzeRequest? request, CancellationToken ct) =>
        OkResponse(await analyses.AnalyzeAsync(resumeId, request ?? new AnalyzeRequest(null, null), ct));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AnalysisListItemDto>>>> List([FromQuery] Guid? resumeId, CancellationToken ct) =>
        OkResponse(await analyses.ListAsync(resumeId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AnalysisDto>>> Get(Guid id, CancellationToken ct) =>
        OkResponse(await analyses.GetAsync(id, ct));

    [HttpGet("{id:guid}/report")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct)
    {
        var (content, fileName) = await analyses.RenderReportAsync(id, ct);
        return File(content, "application/pdf", fileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await analyses.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Analiz silindi."));
    }
}
