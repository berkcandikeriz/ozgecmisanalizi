using System.ComponentModel.DataAnnotations;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.WebUI.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [StringLength(150)]
    [Display(Name = "Ad soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Şehir")]
    public string? City { get; set; }

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ResumeEditViewModel
{
    public Guid Id { get; set; }
    public ResumeSaveRequest Resume { get; set; } = new();

    /// <summary>Beceriler virgülle ayrılmış olarak düzenlenir.</summary>
    public string? SkillsText { get; set; }

    public IReadOnlyList<ProfessionListItemDto> Professions { get; set; } = [];
    public int? LastScore { get; set; }
    public Guid? LastAnalysisId { get; set; }
}

public class UploadViewModel
{
    [Display(Name = "CV başlığı")]
    public string? Title { get; set; }
}

public class TailorViewModel
{
    public ResumeDto Resume { get; set; } = default!;
    public string? JobTitle { get; set; }
    public string? JobDescription { get; set; }
    public TailorResultDto? Result { get; set; }
}

public class JobsViewModel
{
    public JobSearchQuery Query { get; set; } = new();
    public IReadOnlyList<ResumeListItemDto> Resumes { get; set; } = [];
    public JobSearchResultDto? Result { get; set; }
}

public class ApplicationsViewModel
{
    public IReadOnlyList<JobApplicationDto> Items { get; set; } = [];
    public IReadOnlyList<ResumeListItemDto> Resumes { get; set; } = [];
    public JobApplicationSaveRequest NewItem { get; set; } = new();
}

public class CoverLetterIndexViewModel
{
    public IReadOnlyList<CoverLetterDto> Letters { get; set; } = [];
    public IReadOnlyList<ResumeListItemDto> Resumes { get; set; } = [];
    public CoverLetterFormModel Form { get; set; } = new();
}

public class CoverLetterFormModel
{
    [Required(ErrorMessage = "CV seçin.")]
    public Guid ResumeId { get; set; }

    [Required(ErrorMessage = "Pozisyon zorunludur.")]
    public string JobTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şirket zorunludur.")]
    public string Company { get; set; } = string.Empty;

    public string? JobDescription { get; set; }
    public string? Tone { get; set; }
    public Guid? JobApplicationId { get; set; }
}

public class AnalysisViewModel
{
    public AnalysisDto Analysis { get; set; } = default!;
    public IReadOnlyList<ProfessionListItemDto> Professions { get; set; } = [];
}

public class ProfessionsViewModel
{
    public IReadOnlyList<ProfessionListItemDto> Items { get; set; } = [];
    public IReadOnlyList<string> Categories { get; set; } = [];
    public string? Search { get; set; }
    public string? Category { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public int StatusCode { get; set; }
}

public static class Fmt
{
    public static string Stage(ApplicationStage s) => s switch
    {
        ApplicationStage.Kaydedildi => "Kaydedildi",
        ApplicationStage.Hazirlik => "Hazırlık",
        ApplicationStage.Basvuruldu => "Başvuruldu",
        ApplicationStage.Mulakat => "Mülakat",
        ApplicationStage.Teklif => "Teklif",
        ApplicationStage.Reddedildi => "Olumsuz",
        _ => s.ToString()
    };

    public static string Language(LanguageLevel l) => l switch
    {
        LanguageLevel.Baslangic => "Başlangıç",
        LanguageLevel.Orta => "Orta",
        LanguageLevel.Iyi => "İyi",
        LanguageLevel.Ileri => "İleri",
        LanguageLevel.AnaDil => "Ana dil",
        _ => l.ToString()
    };

    public static string Template(ResumeTemplate t) => t switch
    {
        ResumeTemplate.Klasik => "Klasik",
        ResumeTemplate.Modern => "Modern (iki sütun)",
        ResumeTemplate.Kompakt => "Kompakt",
        ResumeTemplate.Profesyonel => "Profesyonel",
        _ => t.ToString()
    };

    /// <summary>Puan için anlamsal ton: iyi / orta / zayıf.</summary>
    public static string Tone(int score) => score >= 75 ? "good" : score >= 55 ? "mid" : "low";

    public static string Money(decimal value) => value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " TL";

    public static string Date(DateTime value) => value.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

    public static string Ago(DateTime? value)
    {
        if (value is null) return string.Empty;
        var span = DateTime.UtcNow - value.Value.ToUniversalTime();
        return span.TotalHours < 1 ? "az önce"
            : span.TotalHours < 24 ? $"{(int)span.TotalHours} saat önce"
            : span.TotalDays < 30 ? $"{(int)span.TotalDays} gün önce"
            : Date(value.Value);
    }
}
