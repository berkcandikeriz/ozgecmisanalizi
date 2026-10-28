using OzgecmisAnalizi.Entities.Common;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.Entities.Concrete;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public string? City { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Resume> Resumes { get; set; } = [];
    public ICollection<JobApplication> Applications { get; set; } = [];
}
