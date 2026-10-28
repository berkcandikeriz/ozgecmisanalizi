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
        }

        return base.SendAsync(request, cancellationToken);
    }
}
