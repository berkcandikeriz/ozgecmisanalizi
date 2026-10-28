namespace OzgecmisAnalizi.Entities.Enums;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public enum ResumeTemplate
{
    Klasik = 0,
    Modern = 1,
    Kompakt = 2,
    Profesyonel = 3
}

public enum ResumeSource
{
    Manual = 0,
    Upload = 1
}

public enum SkillImportance
{
    /// <summary>Olmazsa olmaz.</summary>
    Core = 3,
    /// <summary>Önemli.</summary>
    Important = 2,
    /// <summary>Artı değer.</summary>
    NiceToHave = 1
}

public enum ExperienceLevel
{
    Stajyer = 0,
    Junior = 1,
    Orta = 2,
    Kidemli = 3,
    Lider = 4
}

public enum ApplicationStage
{
    Kaydedildi = 0,
    Hazirlik = 1,
    Basvuruldu = 2,
    Mulakat = 3,
    Teklif = 4,
    Reddedildi = 5
}

public enum LanguageLevel
{
    Baslangic = 1,
    Orta = 2,
    Iyi = 3,
    Ileri = 4,
    AnaDil = 5
}
