using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Entities.Dtos;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>Kayıt, giriş ve oturum bilgisi.</summary>
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    /// <summary>Yeni hesap oluşturur ve JWT döndürür.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest request, CancellationToken ct) =>
        OkResponse(await auth.RegisterAsync(request, ct), "Hesabınız oluşturuldu.");

    /// <summary>E-posta ve şifre ile giriş.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken ct) =>
        OkResponse(await auth.LoginAsync(request, ct));

    /// <summary>Oturum açmış kullanıcının bilgileri.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct) => OkResponse(await auth.GetMeAsync(ct));
}
