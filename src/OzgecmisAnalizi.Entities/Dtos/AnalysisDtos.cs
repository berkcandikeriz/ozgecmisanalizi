using OzgecmisAnalizi.Entities.Content;

namespace OzgecmisAnalizi.Entities.Dtos;

public record AnalyzeRequest(Guid? ProfessionId, string? City, bool UseAi = true);

public record AnalysisDto(
    Guid Id,
    Guid ResumeId,
    string ResumeTitle,
    Guid? ProfessionId,
    string? ProfessionName,
    int OverallScore,
    int ComprehensivenessScore,
    int StructureScore,
    int ClarityScore,
    int AtsScore,
    int SkillMatchScore,
    string Label,
    int TopPercent,
    string Engine,
    AnalysisDetails Details,
    DateTime CreatedAt);

public record AnalysisListItemDto(Guid Id, Guid ResumeId, string ResumeTitle, string? ProfessionName, int OverallScore, string Label, DateTime CreatedAt);
