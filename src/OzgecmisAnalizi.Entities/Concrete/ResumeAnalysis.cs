using OzgecmisAnalizi.Entities.Common;
using OzgecmisAnalizi.Entities.Content;

namespace OzgecmisAnalizi.Entities.Concrete;

public class ResumeAnalysis : BaseEntity, IOwnedEntity
{
    public Guid UserId { get; set; }
    public Guid ResumeId { get; set; }
    public Resume? Resume { get; set; }

    public Guid? ProfessionId { get; set; }
    public Profession? Profession { get; set; }

    public int OverallScore { get; set; }
    public int ComprehensivenessScore { get; set; }
    public int StructureScore { get; set; }
    public int ClarityScore { get; set; }
    public int AtsScore { get; set; }
    public int SkillMatchScore { get; set; }

    /// <summary>"Mükemmel", "Çok iyi", "İyi", "Geliştirilmeli".</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Tahmini dilim, örn. 15 → "ilk %15".</summary>
    public int TopPercent { get; set; }

    /// <summary>"Kural motoru" ya da kullanılan yapay zeka modelinin adı.</summary>
    public string Engine { get; set; } = string.Empty;

    public AnalysisDetails Details { get; set; } = new();
}
