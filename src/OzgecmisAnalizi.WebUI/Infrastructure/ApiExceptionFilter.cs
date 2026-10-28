using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OzgecmisAnalizi.WebUI.Services;

namespace OzgecmisAnalizi.WebUI.Infrastructure;

/// <summary>
/// Controller'lardan taşan <see cref="ApiException"/>'ları kullanıcı dostu davranışa çevirir:
/// 401 → çıkış + giriş sayfası, 404 → bulunamadı sayfası, diğerleri → mesajla geri dönüş.
/// </summary>
public sealed class ApiExceptionFilter(ITempDataDictionaryFactory tempDataFactory, ILogger<ApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is HttpRequestException httpEx)
        {
            logger.LogError(httpEx, "API'ye ulaşılamadı.");
            context.Result = new ViewResult { ViewName = "ApiUnavailable" };
            context.ExceptionHandled = true;
            return;
        }

        if (context.Exception is not ApiException ex)
        {
            return;
        }

        var http = context.HttpContext;
        var tempData = tempDataFactory.GetTempData(http);

        switch (ex.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                await http.SignOutAsync();
                tempData[Alerts.Error] = ex.Message;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = http.Request.Path + http.Request.QueryString });
                break;
            case HttpStatusCode.NotFound:
                context.Result = new ViewResult { ViewName = "NotFound", StatusCode = 404 };
                break;
            default:
                tempData[Alerts.Error] = ex.Errors.Count > 0 ? $"{ex.Message} {string.Join(" ", ex.Errors)}" : ex.Message;
                var referer = http.Request.Headers.Referer.ToString();
                context.Result = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host
                    ? new RedirectResult(uri.PathAndQuery)
                    : new RedirectToActionResult("Index", "Dashboard", null);
                break;
        }

        context.ExceptionHandled = true;
    }
}

public static class Alerts
{
    public const string Success = "AlertSuccess";
    public const string Error = "AlertError";
    public const string Info = "AlertInfo";
}
