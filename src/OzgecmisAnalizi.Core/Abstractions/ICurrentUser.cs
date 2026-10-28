namespace OzgecmisAnalizi.Core.Abstractions;

/// <summary>İsteği yapan kullanıcı. API'de JWT'den doldurulur.</summary>
public interface ICurrentUser
{
    Guid? Id { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }

    /// <summary>Kullanıcı kimliği yoksa <see cref="Exceptions.UnauthorizedAppException"/> fırlatır.</summary>
    Guid RequireId();
}
