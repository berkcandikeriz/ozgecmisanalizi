using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Business.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(CancellationToken ct = default);
}

public interface IResumeService
{
    Task<IReadOnlyList<ResumeListItemDto>> ListAsync(CancellationToken ct = default);
    Task<ResumeDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ResumeDto> CreateAsync(ResumeSaveRequest request, CancellationToken ct = default);
    Task<ResumeDto> UpdateAsync(Guid id, ResumeSaveRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ResumeDto> UploadAsync(Stream file, string fileName, string? title, CancellationToken ct = default);
    Task<ResumeDto> DuplicateAsync(Guid id, CancellationToken ct = default);
    /// <param name="language">Boş ya da "tr" ise CV olduğu gibi; aksi halde önce hedef dile çevrilir (örn. "en").</param>
    Task<(byte[] Content, string FileName)> RenderPdfAsync(Guid id, ResumeTemplate? template, string? language = null, CancellationToken ct = default);

    /// <summary>Desteklenen çıktı dilleri ve etkin çeviri motoru.</summary>
    TranslationOptionsDto GetTranslationOptions();

    /// <summary>CV'yi hedef dile çevirir; istenirse düzenlenebilir bir kopya olarak kaydeder.</summary>
    Task<ResumeTranslationResultDto> TranslateAsync(Guid id, ResumeTranslateRequest request, CancellationToken ct = default);
    Task<TailorResultDto> TailorAsync(Guid id, TailorRequest request, CancellationToken ct = default);
    Task<TailorApplyResultDto> ApplyTailoringAsync(Guid id, TailorApplyRequest request, CancellationToken ct = default);
    Task<ResumeChatResponse> ChatAsync(Guid id, ResumeChatRequest request, CancellationToken ct = default);
}

public interface IAnalysisService
{
    Task<AnalysisDto> AnalyzeAsync(Guid resumeId, AnalyzeRequest request, CancellationToken ct = default);
    Task<AnalysisDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnalysisListItemDto>> ListAsync(Guid? resumeId, CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> RenderReportAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProfessionService
{
    Task<IReadOnlyList<ProfessionListItemDto>> ListAsync(string? search, string? category, CancellationToken ct = default);
    Task<ProfessionDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default);
}

public interface IJobSearchService
{
    Task<JobSearchResultDto> SearchAsync(JobSearchQuery query, CancellationToken ct = default);
}

public interface IApplicationService
{
    Task<IReadOnlyList<JobApplicationDto>> ListAsync(CancellationToken ct = default);
    Task<JobApplicationDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<JobApplicationDto> CreateAsync(JobApplicationSaveRequest request, CancellationToken ct = default);
    Task<JobApplicationDto> UpdateAsync(Guid id, JobApplicationSaveRequest request, CancellationToken ct = default);
    Task<JobApplicationDto> ChangeStageAsync(Guid id, ApplicationStage stage, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICoverLetterService
{
    Task<CoverLetterDto> GenerateAsync(CoverLetterRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CoverLetterDto>> ListAsync(CancellationToken ct = default);
    Task<CoverLetterDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CoverLetterDto> UpdateAsync(Guid id, CoverLetterUpdateRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(byte[] Content, string FileName)> RenderPdfAsync(Guid id, ResumeTemplate? template, CancellationToken ct = default);
}

public interface IResumeImprovementService
{
    /// <summary>Önerileri uygular ama kaydetmez; değişiklik listesini döndürür.</summary>
    Task<ImprovementPreviewDto> PreviewAsync(Guid resumeId, Guid? analysisId, CancellationToken ct = default);

    /// <summary>Seçilen değişiklikleri kaydeder (üzerine veya kopya olarak) ve isteğe bağlı yeniden analiz eder.</summary>
    Task<ImprovementApplyResultDto> ApplyAsync(Guid resumeId, ImprovementApplyRequest request, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}
