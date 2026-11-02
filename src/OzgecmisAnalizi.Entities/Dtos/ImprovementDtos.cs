namespace OzgecmisAnalizi.Entities.Dtos;

/// <summary>Önerilerin CV'ye uygulanmasıyla oluşan tek bir değişiklik.</summary>
/// <param name="Id">Önizleme içinde benzersiz kimlik.</param>
/// <param name="Section">"Kişisel bilgiler", "Özet", "İş deneyimi"...</param>
/// <param name="Label">Değişen alanın okunabilir adı, örn. "Kıdemli Geliştirici · ABC — açıklama".</param>
/// <param name="Path">Uygulama yolu: personal.title, summary, skills, experience:2:description, experienceOrder...</param>
/// <param name="Kind">added | modified | removed | reordered</param>
/// <param name="Format">text | list | order — arayüzde nasıl gösterileceği.</param>
/// <param name="Source">"Kural motoru" ya da yapay zeka modelinin adı.</param>
/// <param name="Tip">Uygulanmayan, yalnızca gösterilen "daha da güçlendirmek için" önerisi.</param>
public record ResumeChangeDto(
    string Id,
    string Section,
    string Label,
    string Path,
    string Kind,
    string Format,
    string? Before,
    string? After,
    string Reason,
    string Source,
    string? Tip = null);

public record ImprovementPreviewDto(
    Guid ResumeId,
    string ResumeTitle,
    Guid? AnalysisId,
    int? Score,
    IReadOnlyList<ResumeChangeDto> Changes,
    IReadOnlyList<string> ManualTodos,
    string Engine);

public class ChangePatch
{
    public string Path { get; set; } = string.Empty;
    public string? After { get; set; }
}

public class ImprovementApplyRequest
{
    public List<ChangePatch> Changes { get; set; } = [];

    /// <summary>true: orijinal CV korunur, iyileştirilmiş kopya oluşturulur.</summary>
    public bool SaveAsCopy { get; set; }

    /// <summary>true: kaydettikten sonra aynı meslekle yeniden analiz edilir.</summary>
    public bool Reanalyze { get; set; } = true;

    public Guid? ProfessionId { get; set; }

    /// <summary>Maaş tahmini için şehir (önceki analizle aynı olmalı).</summary>
    public string? City { get; set; }
}

public record ImprovementApplyResultDto(ResumeDto Resume, Guid? AnalysisId, int? NewScore, int AppliedCount);
