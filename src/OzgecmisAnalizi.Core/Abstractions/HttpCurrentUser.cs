using Microsoft.AspNetCore.Http;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.Core.Extensions;

namespace OzgecmisAnalizi.Core.Abstractions;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Id => accessor.HttpContext?.User.GetUserId();
    public string? Email => accessor.HttpContext?.User.GetEmail();
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    public Guid RequireId() => Id ?? throw new UnauthorizedAppException();
}
