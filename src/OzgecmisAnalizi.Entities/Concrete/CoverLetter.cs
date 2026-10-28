using OzgecmisAnalizi.Entities.Common;

namespace OzgecmisAnalizi.Entities.Concrete;

public class CoverLetter : BaseEntity, IOwnedEntity
{
    public Guid UserId { get; set; }
    public Guid? ResumeId { get; set; }
    public Resume? Resume { get; set; }
    public Guid? JobApplicationId { get; set; }

    public string JobTitle { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
}
