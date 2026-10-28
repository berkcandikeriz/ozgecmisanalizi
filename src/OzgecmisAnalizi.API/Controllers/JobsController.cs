using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>CV'ye uygun iş ilanları ve LinkedIn / Indeed / Kariyer.net arama bağlantıları.</summary>
[Route("api/jobs")]
public sealed class JobsController(IJobSearchService jobs) : ApiControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<JobSearchResultDto>>> Search([FromQuery] JobSearchQuery query, CancellationToken ct) =>
        OkResponse(await jobs.SearchAsync(query, ct));
}
