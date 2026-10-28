using OzgecmisAnalizi.Entities.Common;

namespace OzgecmisAnalizi.Entities.Concrete;

/// <summary>Meslek kataloğu. Seed verisiyle doldurulur, yönetici güncelleyebilir.</summary>
public class Profession : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>İlan aramasında kullanılacak anahtar kelimeler (virgüllü).</summary>
    public string SearchKeywords { get; set; } = string.Empty;

    /// <summary>Junior seviye net maaşın asgari ücrete oranı (örn. 1.4).</summary>
    public decimal JuniorSalaryFactor { get; set; } = 1.2m;
    /// <summary>Kıdemli seviye net maaşın asgari ücrete oranı (örn. 4.0).</summary>
    public decimal SeniorSalaryFactor { get; set; } = 2.5m;

    /// <summary>Seviye başlıkları, "|" ile ayrılmış: Stajyer|Junior|Orta|Kıdemli|Lider.</summary>
    public string CareerTitles { get; set; } = string.Empty;

    public ICollection<ProfessionSkill> Skills { get; set; } = [];
}

public class ProfessionSkill : BaseEntity
{
    public Guid ProfessionId { get; set; }
    public Profession? Profession { get; set; }

    public string Name { get; set; } = string.Empty;
    /// <summary>Eşleştirme için eş anlamlılar (virgüllü), örn. "c#,csharp,c sharp".</summary>
    public string Aliases { get; set; } = string.Empty;
    public Enums.SkillImportance Importance { get; set; } = Enums.SkillImportance.Important;
    public string? LearningHint { get; set; }
}
