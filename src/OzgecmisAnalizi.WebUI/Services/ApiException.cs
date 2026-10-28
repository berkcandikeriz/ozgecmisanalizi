using System.Net;

namespace OzgecmisAnalizi.WebUI.Services;

/// <summary>API'nin başarısız döndürdüğü yanıtlar. Mesaj kullanıcıya gösterilebilir.</summary>
public sealed class ApiException(HttpStatusCode statusCode, string message, IReadOnlyList<string>? errors = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyList<string> Errors { get; } = errors ?? [];
}
