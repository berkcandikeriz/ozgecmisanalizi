using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Entities.Content;

/// <summary>Yapılandırılmış CV içeriği. Veritabanında tek JSON kolonu olarak saklanır, API'de aynen taşınır.</summary>
public class ResumeContent
{
    public PersonalInfo Personal { get; set; } = new();
    public string? Summary { get; set; }
    public List<ExperienceItem> Experiences { get; set; } = [];
    public List<EducationItem> Educations { get; set; } = [];
    public List<string> Skills { get; set; } = [];
    public List<LanguageItem> Languages { get; set; } = [];
    public List<CertificateItem> Certificates { get; set; } = [];
    public List<ProjectItem> Projects { get; set; } = [];
}

public class PersonalInfo
{
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? LinkedIn { get; set; }
    public string? Website { get; set; }
}

public class ExperienceItem
{
    public string? Position { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    /// <summary>"2021-03" veya "2021" biçiminde.</summary>
    public string? StartDate { get; set; }
    /// <summary>Boşsa devam ediyor kabul edilir.</summary>
    public string? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
}

public class EducationItem
{
    public string? School { get; set; }
    public string? Degree { get; set; }
    public string? Field { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Grade { get; set; }
}

public class LanguageItem
{
    public string? Name { get; set; }
    public LanguageLevel Level { get; set; } = LanguageLevel.Orta;
}

public class CertificateItem
{
    public string? Name { get; set; }
    public string? Issuer { get; set; }
    public string? Date { get; set; }
}

public class ProjectItem
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}
