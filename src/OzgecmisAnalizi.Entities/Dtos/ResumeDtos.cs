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

/// <summary>Kaydedilmemiş editör içeriğinden PDF önizlemesi.</summary>
public class ResumePreviewRequest
{
    public ResumeTemplate Template { get; set; } = ResumeTemplate.Modern;
    public ResumeContent Content { get; set; } = new();
    public bool IncludePhoto { get; set; } = true;
}

/// <param name="PageCount">PDF'in toplam sayfa sayısı.</param>
/// <param name="Pages">Sayfa görselleri (data:image/jpeg;base64,...); en fazla ilk birkaç sayfa.</param>
public record ResumePreviewDto(int PageCount, IReadOnlyList<string> Pages);

public record TailorRequest(string JobDescription, string? JobTitle);

public record TailorResultDto(
    int MatchPercent,
    IReadOnlyList<string> MatchedKeywords,
    IReadOnlyList<string> MissingKeywords,
    IReadOnlyList<string> Suggestions,
    string? SuggestedSummary,
    string Engine);

/// <summary>İlana göre uyarlanmış bir CV kopyası oluşturur; isteğe bağlı olarak başvuru panosuna ekler.</summary>
public class TailorApplyRequest
{
    public string JobTitle { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? JobUrl { get; set; }
    public string? JobDescription { get; set; }

    /// <summary>Kullanıcının "bende var" diye onayladığı eksik anahtar kelimeler; beceriler listesine eklenir.</summary>
    public List<string> AddKeywords { get; set; } = [];

    /// <summary>Doluysa özet bölümü bununla değiştirilir.</summary>
    public string? Summary { get; set; }

    public bool UpdateTitle { get; set; } = true;
    public bool CreateApplication { get; set; } = true;
}

public record TailorApplyResultDto(ResumeDto Resume, Guid? ApplicationId);

public record ChatMessageDto(string Role, string Content);

public record ResumeChatRequest(IReadOnlyList<ChatMessageDto> Messages);

public record ResumeChatResponse(string Reply, string Engine);
