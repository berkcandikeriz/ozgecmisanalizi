using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OzgecmisAnalizi.Core.Results;

namespace OzgecmisAnalizi.WebUI.Services;

public sealed record FileResultData(byte[] Content, string ContentType, string FileName);

/// <summary>
/// OzgecmisAnalizi.API için tipli istemci. Tüm yanıtlar <see cref="ApiResponse{T}"/> zarfında gelir;
/// başarısız yanıtlar <see cref="ApiException"/> olarak fırlatılır.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = CreateJson();

    public Task<T> GetAsync<T>(string url, CancellationToken ct = default) => SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url), ct);

    public Task<T> PostAsync<T>(string url, object? body, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    public Task<T> PutAsync<T>(string url, object body, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    public Task<T> PatchAsync<T>(string url, object body, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        using var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url), ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<T> UploadAsync<T>(string url, Stream file, string fileName, string contentType, IDictionary<string, string?> fields, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var mt) ? mt : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        foreach (var (key, value) in fields)
        {
            if (value is not null)
            {
                form.Add(new StringContent(value), key);
            }
        }

        return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, url) { Content = form }, ct);
    }

    public async Task<FileResultData> GetFileAsync(string url, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(url, ct);
        await EnsureSuccessAsync(response, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar
                   ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                   ?? "dosya.pdf";
        return new FileResultData(bytes, response.Content.Headers.ContentType?.ToString() ?? "application/pdf", name);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        using (var response = await http.SendAsync(request, ct))
        {
            await EnsureSuccessAsync(response, ct);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(Json, ct);
            if (envelope is null || !envelope.Success)
            {
                throw new ApiException(response.StatusCode, envelope?.Message ?? "Sunucudan geçersiz yanıt alındı.", envelope?.Errors);
            }

            return envelope.Data!;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message;
        IReadOnlyList<string>? errors = null;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json, ct);
            message = body?.Message ?? DefaultMessage(response.StatusCode);
            errors = body?.Errors;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            message = DefaultMessage(response.StatusCode);
        }

        throw new ApiException(response.StatusCode, message, errors);
    }

    private static string DefaultMessage(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "Oturumunuzun süresi doldu. Lütfen yeniden giriş yapın.",
        HttpStatusCode.Forbidden => "Bu işlem için yetkiniz yok.",
        HttpStatusCode.NotFound => "Aradığınız kayıt bulunamadı.",
        HttpStatusCode.TooManyRequests => "Çok fazla deneme yapıldı. Bir dakika sonra tekrar deneyin.",
        _ => "Sunucuya ulaşılırken bir sorun oluştu. Lütfen tekrar deneyin."
    };

    private static JsonSerializerOptions CreateJson()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}
