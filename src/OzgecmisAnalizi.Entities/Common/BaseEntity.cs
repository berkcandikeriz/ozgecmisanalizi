namespace OzgecmisAnalizi.Entities.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Bir kullanıcıya ait olan kayıtlar. Erişim kontrolü bu alan üzerinden yapılır.</summary>
public interface IOwnedEntity
{
    Guid UserId { get; }
}
