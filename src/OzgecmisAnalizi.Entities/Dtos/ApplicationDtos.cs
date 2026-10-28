using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Entities.Dtos;

public record JobApplicationDto(
    Guid Id,
    string JobTitle,
    string Company,
    string? Location,
    string? Url,
    string? Source,
    string? Description,
    ApplicationStage Stage,
    int? MatchScore,
    Guid? ResumeId,
    DateTime? AppliedAt,
    DateTime? InterviewAt,
    string? Notes,
    DateTime CreatedAt);

public class JobApplicationSaveRequest
{
    public string JobTitle { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
    public string? Description { get; set; }
    public ApplicationStage Stage { get; set; } = ApplicationStage.Kaydedildi;
    public int? MatchScore { get; set; }
    public Guid? ResumeId { get; set; }
    public DateTime? InterviewAt { get; set; }
    public string? Notes { get; set; }
}

public record StageChangeRequest(ApplicationStage Stage);
