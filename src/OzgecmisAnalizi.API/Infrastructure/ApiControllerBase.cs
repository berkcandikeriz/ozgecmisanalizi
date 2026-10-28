using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Core.Results;

namespace OzgecmisAnalizi.API.Infrastructure;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data, string? message = null) => Ok(ApiResponse<T>.Ok(data, message));

    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(string actionName, object routeValues, T data) =>
        CreatedAtAction(actionName, routeValues, ApiResponse<T>.Ok(data));
}
