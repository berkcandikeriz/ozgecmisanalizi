using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.Core.Extensions;
using OzgecmisAnalizi.Core.Tools;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Services;

public sealed class ApplicationService(IUnitOfWork uow, ICurrentUser currentUser) : IApplicationService
{
    private IRepository<JobApplication> Applications => uow.Repository<JobApplication>();

    public async Task<IReadOnlyList<JobApplicationDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        var rows = await Applications.Query().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<JobApplicationDto> GetAsync(Guid id, CancellationToken ct = default) => (await LoadAsync(id, false, ct)).ToDto();

    public async Task<JobApplicationDto> CreateAsync(JobApplicationSaveRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.RequireId();
        if (request.Url.HasValue() && await Applications.AnyAsync(a => a.UserId == userId && a.Url == request.Url, ct))
        {
            var existing = await Applications.Query().FirstAsync(a => a.UserId == userId && a.Url == request.Url, ct);
            return existing.ToDto();
        }

        var entity = new JobApplication { UserId = userId };
        await ApplyAsync(entity, request, userId, ct);
        await Applications.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<JobApplicationDto> UpdateAsync(Guid id, JobApplicationSaveRequest request, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, true, ct);
        await ApplyAsync(entity, request, entity.UserId, ct);
        await uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<JobApplicationDto> ChangeStageAsync(Guid id, ApplicationStage stage, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, true, ct);
        SetStage(entity, stage);
        await uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, true, ct);
        Applications.Remove(entity);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<JobApplication> LoadAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var userId = currentUser.RequireId();
        var q = tracked ? Applications.Tracked() : Applications.Query();
        return Guard.Found(await q.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct), "Başvuru", id);
    }

    private async Task ApplyAsync(JobApplication e, JobApplicationSaveRequest r, Guid userId, CancellationToken ct)
    {
        e.JobTitle = Guard.Required(r.JobTitle, "Pozisyon", 200);
        e.Company = Guard.Required(r.Company, "Şirket", 200);
        e.Location = r.Location?.Trim().Truncate(150);
        e.Url = r.Url?.Trim().Truncate(1000);
        Guard.Against(e.Url.HasValue() && !Uri.TryCreate(e.Url, UriKind.Absolute, out _), "İlan bağlantısı geçerli bir adres olmalıdır.");
        e.Source = r.Source?.Trim().Truncate(60);
        e.Description = r.Description?.Trim().Truncate(20000);
        e.MatchScore = r.MatchScore is null ? null : Math.Clamp(r.MatchScore.Value, 0, 100);
        e.InterviewAt = r.InterviewAt;
        e.Notes = r.Notes?.Trim().Truncate(4000);

        if (r.ResumeId is { } resumeId)
        {
            var owns = await uow.Repository<Resume>().AnyAsync(x => x.Id == resumeId && x.UserId == userId, ct);
            Guard.Against(!owns, "Seçilen CV bulunamadı.");
        }

        e.ResumeId = r.ResumeId;
        SetStage(e, r.Stage);
    }

    private static void SetStage(JobApplication e, ApplicationStage stage)
    {
        if (stage >= ApplicationStage.Basvuruldu && e.AppliedAt is null && stage != ApplicationStage.Reddedildi)
        {
            e.AppliedAt = DateTime.UtcNow;
        }

        e.Stage = stage;
    }
}
