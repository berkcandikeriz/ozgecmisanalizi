namespace OzgecmisAnalizi.Core.Results;

/// <summary>Tüm API yanıtları için ortak zarf. Farklı istemciler (MVC, Angular, Flutter) aynı sözleşmeyi okur.</summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public string? TraceId { get; init; }

    public static ApiResponse Ok(string? message = null) => new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, IReadOnlyList<string>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new() { Success = true, Data = data, Message = message };

    public static new ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null, string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}
