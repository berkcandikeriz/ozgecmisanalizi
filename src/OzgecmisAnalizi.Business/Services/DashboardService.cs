using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.Business.Abstractions;
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

        var byStage = Enum.GetValues<ApplicationStage>().ToDictionary(s => s.ToString(), s => stages.FirstOrDefault(x => x.Stage == s)?.Count ?? 0);

        return new DashboardDto(
            resumeList.Count,
            analysisList.Count,
            stages.Sum(s => s.Count),
            analysisList.Count == 0 ? null : analysisList.Max(a => a.OverallScore),
            resumeList.Take(5).ToList(),
            analysisList.Take(5).ToList(),
            byStage);
    }
}
