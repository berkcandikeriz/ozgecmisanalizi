using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OzgecmisAnalizi.Core.Exceptions;
using OzgecmisAnalizi.Core.Results;

namespace OzgecmisAnalizi.Core.Middleware;

/// <summary>Yakalanmayan tüm hataları loglar ve <see cref="ApiResponse"/> formatında döndürür.</summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // İstemci bağlantıyı kapattı; loglamaya gerek yok.
            context.Response.StatusCode = 499;
        }
        catch (AppException ex)
        {
            logger.LogWarning(ex, "İş kuralı hatası: {Message}", ex.Message);
            await WriteAsync(context, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Beklenmeyen hata. Yol: {Path}", context.Request.Path);
            var message = environment.IsDevelopment()
                ? ex.Message
                : "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
            await WriteAsync(context, HttpStatusCode.InternalServerError, message, null);
        }
    }

    private static async Task WriteAsync(HttpContext context, HttpStatusCode status, string message, IReadOnlyList<string>? errors)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = ApiResponse.Fail(message, errors is { Count: > 0 } ? errors : null, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
