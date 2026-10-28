using System.Security.Claims;

namespace OzgecmisAnalizi.Core.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string? GetEmail(this ClaimsPrincipal? principal) =>
        principal?.FindFirstValue(ClaimTypes.Email) ?? principal?.FindFirstValue("email");

    public static string? GetFullName(this ClaimsPrincipal? principal) =>
        principal?.FindFirstValue(ClaimTypes.Name) ?? principal?.FindFirstValue("name");
}
