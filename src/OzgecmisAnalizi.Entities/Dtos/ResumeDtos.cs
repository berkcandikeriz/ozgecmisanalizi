using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Entities.Dtos;

public record ResumeListItemDto(
    Guid Id,
    string Title,
    ResumeTemplate Template,
    ResumeSource Source,
    string? TargetProfession,
    int? LastScore,
    Guid? LastAnalysisId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ResumeDto(
    Guid Id,
    string Title,
    ResumeTemplate Template,
    ResumeSource Source,
    Guid? TargetProfessionId,
    string? OriginalFileName,
    ResumeContent Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class ResumeSaveRequest
{
    public string Title { get; set; } = "CV'm";
    public ResumeTemplate Template { get; set; } = ResumeTemplate.Modern;
    public Guid? TargetProfessionId { get; set; }
    public ResumeContent Content { get; set; } = new();
}

public record TailorRequest(string JobDescription, string? JobTitle);

public record TailorResultDto(
    int MatchPercent,
    IReadOnlyList<string> MatchedKeywords,
    IReadOnlyList<string> MissingKeywords,
    IReadOnlyList<string> Suggestions,
    string? SuggestedSummary,
    string Engine);
