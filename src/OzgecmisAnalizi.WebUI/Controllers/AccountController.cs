using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.WebUI.Infrastructure;
using OzgecmisAnalizi.WebUI.Models;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Controllers;

[Route("hesap")]
public sealed class AccountController(ApiClient api) : Controller
{
    [HttpGet("giris")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl) =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost("giris")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = await api.PostAsync<AuthResponse>("api/auth/login", new LoginRequest(model.Email, model.Password), ct);
            await SignInAsync(auth);
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/panel");
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet("kayit")]
    [AllowAnonymous]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View(new RegisterViewModel());

    [HttpPost("kayit")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = await api.PostAsync<AuthResponse>("api/auth/register", new RegisterRequest(model.FullName, model.Email, model.Password, model.City), ct);
            await SignInAsync(auth);
            TempData[Alerts.Success] = "Hesabınız hazır. İlk CV'nizi yükleyerek başlayın.";
            return RedirectToAction("Upload", "Resume");
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            foreach (var e in ex.Errors)
            {
                ModelState.AddModelError(string.Empty, e);
            }

            return View(model);
        }
    }

    [HttpPost("cikis")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    private async Task SignInAsync(AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.Id.ToString()),
            new(ClaimTypes.Name, auth.User.FullName),
            new(ClaimTypes.Email, auth.User.Email),
            new(ClaimTypes.Role, auth.User.Role),
            new(BearerTokenHandler.TokenClaim, auth.Token)
        };

        if (!string.IsNullOrWhiteSpace(auth.User.City))
        {
            claims.Add(new Claim("city", auth.User.City));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = auth.ExpiresAt });
    }
}
