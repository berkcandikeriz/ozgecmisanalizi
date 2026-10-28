namespace OzgecmisAnalizi.Entities.Dtos;

public record DashboardDto(
    int ResumeCount,
    int AnalysisCount,
    int ApplicationCount,
    int? BestScore,
    IReadOnlyList<ResumeListItemDto> RecentResumes,
    IReadOnlyList<AnalysisListItemDto> RecentAnalyses,
    IReadOnlyDictionary<string, int> ApplicationsByStage);
