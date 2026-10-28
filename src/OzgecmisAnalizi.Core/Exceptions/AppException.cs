using System.Net;

namespace OzgecmisAnalizi.Core.Exceptions;

/// <summary>Uygulamanın bilinçli olarak fırlattığı tüm hataların tabanı. Middleware bunları uygun HTTP koduna çevirir.</summary>
public abstract class AppException(string message, HttpStatusCode statusCode, IReadOnlyList<string>? errors = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyList<string> Errors { get; } = errors ?? [];
}

public sealed class NotFoundException(string entity, object key)
    : AppException($"{entity} bulunamadı ({key}).", HttpStatusCode.NotFound);

public sealed class ValidationException(IReadOnlyList<string> errors)
    : AppException("Gönderilen bilgiler geçersiz.", HttpStatusCode.BadRequest, errors)
{
    public ValidationException(string error) : this([error]) { }
}

public sealed class BusinessException(string message)
    : AppException(message, HttpStatusCode.UnprocessableEntity);

public sealed class ConflictException(string message)
    : AppException(message, HttpStatusCode.Conflict);

public sealed class UnauthorizedAppException(string message = "Bu işlem için giriş yapmalısınız.")
    : AppException(message, HttpStatusCode.Unauthorized);

public sealed class ForbiddenException(string message = "Bu kayda erişim yetkiniz yok.")
    : AppException(message, HttpStatusCode.Forbidden);

public sealed class ExternalServiceException(string service, string message)
    : AppException($"{service}: {message}", HttpStatusCode.BadGateway);
