using Microsoft.AspNetCore.Builder;

namespace OzgecmisAnalizi.Core.Middleware;

public static class MiddlewareExtensions
{
    /// <summary>Korelasyon kimliği + global hata yönetimi. Pipeline'ın en başına eklenmelidir.</summary>
    public static IApplicationBuilder UseCoreMiddlewares(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>()
           .UseMiddleware<GlobalExceptionMiddleware>();
}
