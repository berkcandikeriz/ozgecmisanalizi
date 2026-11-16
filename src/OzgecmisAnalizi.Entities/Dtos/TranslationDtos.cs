namespace OzgecmisAnalizi.Entities.Dtos;

/// <summary>CV çıktısı için desteklenen dil. <paramref name="Code"/> ISO 639-1 (örn. "en").</summary>
public record TranslationLanguageDto(string Code, string Name, string NativeName);

/// <summary>Çeviri ekranı için diller ve etkin çeviri motoru.</summary>
/// <param name="FullTranslation">Yapay zeka veya LibreTranslate tanımlıysa true: özet ve deneyim maddeleri de çevrilir.</param>
public record TranslationOptionsDto(IReadOnlyList<TranslationLanguageDto> Languages, string Engine, bool FullTranslation);

public class ResumeTranslateRequest
{
    /// <summary>Hedef dil kodu, örn. "en".</summary>
    public string Language { get; set; } = "en";

    /// <summary>Çevrilmiş içerik yeni bir CV olarak kaydedilsin mi (düzenlenebilir kopya).</summary>
    public bool SaveAsCopy { get; set; } = true;

    /// <summary>Çevrilmiş kopyada vesikalık fotoğraf korunsun mu (ABD/İngiltere/Kanada için genellikle false).</summary>
    public bool IncludePhoto { get; set; } = true;
}

/// <param name="TranslatedCount">Çevrilen metin parçası sayısı.</param>
/// <param name="TotalCount">Çevrilebilir metin parçası sayısı.</param>
/// <param name="Note">Eksik çeviri varsa kullanıcıya gösterilecek açıklama.</param>
public record ResumeTranslationResultDto(
    string Language,
    string Engine,
    bool Complete,
    int TranslatedCount,
    int TotalCount,
    string? Note,
    ResumeDto? Copy);
