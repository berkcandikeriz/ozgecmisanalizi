using System.Net.Http.Headers;

namespace OzgecmisAnalizi.WebUI.Services;

/// <summary>Oturum çerezindeki API anahtarını (JWT) her isteğe ekler; korelasyon kimliğini taşır.</summary>
public sealed class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    public const string TokenClaim = "access_token";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;
        var token = context?.User.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (context is not null)
        {
            request.Headers.TryAddWithoutValidation(Core.Middleware.CorrelationIdMiddleware.HeaderName, context.TraceIdentifier);

            // Bazı ilan API'leri (Careerjet) son kullanıcının IP ve tarayıcı bilgisini ister; API'ye iletilir.
            if (context.Connection.RemoteIpAddress is { } ip)
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip.ToString());
            }

            if (context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua)
            {
                request.Headers.TryAddWithoutValidation("X-Client-User-Agent", ua);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
