using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>Başvuru takip panosu.</summary>
[Route("api/applications")]
public sealed class ApplicationsController(IApplicationService applications) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JobApplicationDto>>>> List(CancellationToken ct) =>
        OkResponse(await applications.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JobApplicationDto>>> Get(Guid id, CancellationToken ct) =>
        OkResponse(await applications.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<JobApplicationDto>>> Create(JobApplicationSaveRequest request, CancellationToken ct)
    {
        var dto = await applications.CreateAsync(request, ct);
        return CreatedResponse(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JobApplicationDto>>> Update(Guid id, JobApplicationSaveRequest request, CancellationToken ct) =>
        OkResponse(await applications.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/stage")]
    public async Task<ActionResult<ApiResponse<JobApplicationDto>>> ChangeStage(Guid id, StageChangeRequest request, CancellationToken ct) =>
        OkResponse(await applications.ChangeStageAsync(id, request.Stage, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await applications.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Başvuru silindi."));
    }
}
