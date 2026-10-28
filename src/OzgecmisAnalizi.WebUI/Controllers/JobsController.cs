using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

public sealed class JobsController(ApiClient api) : AppController(api)
{
    [HttpGet("/isler")]
    public async Task<IActionResult> Index([FromQuery] JobSearchQuery query, CancellationToken ct)
    {
        var resumes = await Api.GetAsync<IReadOnlyList<ResumeListItemDto>>("api/resumes", ct);
        var vm = new JobsViewModel { Query = query, Resumes = resumes };

        if (query.ResumeId is null && string.IsNullOrWhiteSpace(query.Keywords))
        {
            query.ResumeId = resumes.FirstOrDefault()?.Id;
        }

        if (query.ResumeId is not null || !string.IsNullOrWhiteSpace(query.Keywords))
        {
            try
            {
                var qs = QueryString.Create(new Dictionary<string, string?>
                {
                    ["keywords"] = query.Keywords,
                    ["location"] = query.Location,
                    ["resumeId"] = query.ResumeId?.ToString(),
                    ["page"] = query.Page.ToString()
                });
                vm.Result = await Api.GetAsync<JobSearchResultDto>("api/jobs/search" + qs, ct);
                vm.Query.Keywords ??= vm.Result.Keywords;
                vm.Query.Location ??= vm.Result.Location;
            }
            catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest)
            {
                Error(ex.Errors.FirstOrDefault() ?? ex.Message);
            }
        }

        return View(vm);
    }

    [HttpPost("/isler/takibe-al")]
    public async Task<IActionResult> Track(JobApplicationSaveRequest request, string? returnUrl, CancellationToken ct)
    {
        request.Stage = ApplicationStage.Kaydedildi;
        await Api.PostAsync<JobApplicationDto>("api/applications", request, ct);
        Success($"\"{request.JobTitle}\" başvuru panonuza eklendi.");
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
