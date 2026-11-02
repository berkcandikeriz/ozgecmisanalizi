using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Services;

public sealed class DashboardService(IUnitOfWork uow, ICurrentUser currentUser, IResumeService resumes, IAnalysisService analyses) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var resumeList = await resumes.ListAsync(ct);
        var analysisList = await analyses.ListAsync(null, ct);
        var stages = await uow.Repository<JobApplication>().Query()
            .Where(a => a.UserId == userId)
            .GroupBy(a => a.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStage = Enum.GetValues<ApplicationStage>()
            .Where(s => s != ApplicationStage.UygunDegil)
            .ToDictionary(s => s.ToString(), s => stages.FirstOrDefault(x => x.Stage == s)?.Count ?? 0);

        var now = DateTime.UtcNow.AddHours(-2);
        var upcoming = await uow.Repository<JobApplication>().Query()
            .Where(a => a.UserId == userId && a.InterviewAt != null && a.InterviewAt >= now)
            .OrderBy(a => a.InterviewAt)
            .Take(5)
            .ToListAsync(ct);

        return new DashboardDto(
            resumeList.Count,
            analysisList.Count,
            stages.Where(s => s.Stage != ApplicationStage.UygunDegil).Sum(s => s.Count),
            analysisList.Count == 0 ? null : analysisList.Max(a => a.OverallScore),
            resumeList.Take(5).ToList(),
            analysisList.Take(5).ToList(),
            byStage,
            upcoming.Select(a => a.ToDto()).ToList());
    }
}
