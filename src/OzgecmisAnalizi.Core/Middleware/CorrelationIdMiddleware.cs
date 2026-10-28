using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace OzgecmisAnalizi.Core.Middleware;

/// <summary>Her isteğe bir korelasyon kimliği verir; loglarda ve yanıt başlığında taşır.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : context.TraceIdentifier;

        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }
}
