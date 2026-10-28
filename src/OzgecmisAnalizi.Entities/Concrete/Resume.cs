using OzgecmisAnalizi.Entities.Common;
using OzgecmisAnalizi.Entities.Content;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Entities.Concrete;

public class Resume : BaseEntity, IOwnedEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Title { get; set; } = "CV'm";
    public ResumeTemplate Template { get; set; } = ResumeTemplate.Modern;
    public ResumeSource Source { get; set; } = ResumeSource.Manual;
    public Guid? TargetProfessionId { get; set; }
    public Profession? TargetProfession { get; set; }

    public ResumeContent Content { get; set; } = new();

    /// <summary>Yüklenen dosyanın adı ve ondan çıkarılan ham metin (analiz için).</summary>
    public string? OriginalFileName { get; set; }
    public string? RawText { get; set; }

    public ICollection<ResumeAnalysis> Analyses { get; set; } = [];
}
