using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Mapping;
using OzgecmisAnalizi.Core.Abstractions;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.Core.Tools;
using OzgecmisAnalizi.DataAccess.Repositories;
using OzgecmisAnalizi.Entities.Concrete;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.Business.Services;

public sealed class AuthService(
    IUnitOfWork uow,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    ICurrentUser currentUser) : IAuthService
{
    private IRepository<User> Users => uow.Repository<User>();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var fullName = Guard.Required(request.FullName, "Ad soyad", 150);
        var email = Guard.Required(request.Email, "E-posta", 256);
        Guard.Against(!email.Contains('@') || !email.Contains('.'), "Geçerli bir e-posta adresi girin.");
        Guard.Against(string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8, "Şifre en az 8 karakter olmalıdır.");
        Guard.Against(!request.Password.Any(char.IsDigit) || !request.Password.Any(char.IsLetter), "Şifre en az bir harf ve bir rakam içermelidir.");

        var normalized = email.ToUpperInvariant();
        if (await Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
        {
            throw new ConflictException("Bu e-posta adresiyle zaten bir hesap var.");
        }

        var user = new User { FullName = fullName, Email = email, NormalizedEmail = normalized, City = request.City?.Trim() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        user.LastLoginAt = DateTime.UtcNow;
        await Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return Issue(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalized = (request.Email ?? string.Empty).Trim().ToUpperInvariant();
        var user = await Users.Tracked().FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (user is null)
        {
            throw new UnauthorizedAppException("E-posta veya şifre hatalı.");
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAppException("E-posta veya şifre hatalı.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return Issue(user);
    }

    public async Task<UserDto> GetMeAsync(CancellationToken ct = default)
    {
        var id = currentUser.RequireId();
        var user = await Users.Query().FirstOrDefaultAsync(u => u.Id == id, ct);
        return Guard.Found(user, "Kullanıcı", id).ToDto();
    }

    private AuthResponse Issue(User user)
    {
        var (token, expires) = tokens.Create(user);
        return new AuthResponse(token, expires, user.ToDto());
    }
}
