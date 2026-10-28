using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.Core.Results;

namespace OzgecmisAnalizi.API.Infrastructure;

public static class ValidationResponseFactory
{
    /// <summary>Model doğrulama hatalarını da ortak <see cref="ApiResponse"/> zarfında döndürür.</summary>
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .SelectMany(kv => kv.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? $"{kv.Key} geçersiz." : e.ErrorMessage))
            .ToList();
        return new BadRequestObjectResult(ApiResponse.Fail("Gönderilen bilgiler geçersiz.", errors, context.HttpContext.TraceIdentifier));
    }
}
