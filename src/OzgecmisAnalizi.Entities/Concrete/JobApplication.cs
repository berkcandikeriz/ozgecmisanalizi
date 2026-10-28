using OzgecmisAnalizi.Entities.Common;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Entities.Concrete;

public class JobApplication : BaseEntity, IOwnedEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? ResumeId { get; set; }
    public Resume? Resume { get; set; }

    public string JobTitle { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
    public string? Description { get; set; }
    public ApplicationStage Stage { get; set; } = ApplicationStage.Kaydedildi;
    public int? MatchScore { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? InterviewAt { get; set; }
    public string? Notes { get; set; }
}
