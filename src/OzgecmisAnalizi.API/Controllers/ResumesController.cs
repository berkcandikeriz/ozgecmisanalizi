using Microsoft.AspNetCore.Mvc;
using OzgecmisAnalizi.API.Infrastructure;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Core.Results;
using OzgecmisAnalizi.Core.Tools;
using OzgecmisAnalizi.Entities.Dtos;
using OzgecmisAnalizi.Entities.Enums;

namespace OzgecmisAnalizi.API.Controllers;

/// <summary>CV oluşturma, yükleme, düzenleme, PDF ve ilana göre uyarlama.</summary>
[Route("api/resumes")]
public sealed class ResumesController(IResumeService resumes, IResumeImprovementService improvements) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ResumeListItemDto>>>> List(CancellationToken ct) =>
        OkResponse(await resumes.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ResumeDto>>> Get(Guid id, CancellationToken ct) =>
        OkResponse(await resumes.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ResumeDto>>> Create(ResumeSaveRequest request, CancellationToken ct)
    {
        var dto = await resumes.CreateAsync(request, ct);
        return CreatedResponse(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ResumeDto>>> Update(Guid id, ResumeSaveRequest request, CancellationToken ct) =>
        OkResponse(await resumes.UpdateAsync(id, request, ct), "CV kaydedildi.");

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        await resumes.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("CV silindi."));
    }

    /// <summary>PDF, DOCX veya TXT CV yükler; metni ayrıştırıp düzenlenebilir CV oluşturur. En fazla 10 MB.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(FileGuard.MaxResumeBytes + 64 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ResumeDto>>> Upload(IFormFile file, [FromForm] string? title, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var dto = await resumes.UploadAsync(stream, file.FileName, title, ct);
        return CreatedResponse(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<ApiResponse<ResumeDto>>> Duplicate(Guid id, CancellationToken ct) =>
        OkResponse(await resumes.DuplicateAsync(id, ct));

    /// <summary>CV'yi seçilen şablonla PDF olarak indirir.</summary>
    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] ResumeTemplate? template, CancellationToken ct)
    {
        var (content, fileName) = await resumes.RenderPdfAsync(id, template, ct);
        return File(content, "application/pdf", fileName);
    }

    /// <summary>CV'yi bir iş ilanıyla karşılaştırır; eksik anahtar kelimeler ve öneriler döndürür.</summary>
    [HttpPost("{id:guid}/tailor")]
    public async Task<ActionResult<ApiResponse<TailorResultDto>>> Tailor(Guid id, TailorRequest request, CancellationToken ct) =>
        OkResponse(await resumes.TailorAsync(id, request, ct));

    /// <summary>İlana uyarlanmış bir CV kopyası oluşturur ve isteğe bağlı olarak başvuru panosuna ekler.</summary>
    [HttpPost("{id:guid}/tailor/apply")]
    public async Task<ActionResult<ApiResponse<TailorApplyResultDto>>> ApplyTailoring(Guid id, TailorApplyRequest request, CancellationToken ct) =>
        OkResponse(await resumes.ApplyTailoringAsync(id, request, ct), "Uyarlanmış CV oluşturuldu.");

    /// <summary>Analiz önerilerini CV'ye uygular ama kaydetmez; önce/sonra değişiklik listesi döndürür.</summary>
    [HttpPost("{id:guid}/improve")]
    public async Task<ActionResult<ApiResponse<ImprovementPreviewDto>>> ImprovePreview(Guid id, [FromQuery] Guid? analysisId, CancellationToken ct) =>
        OkResponse(await improvements.PreviewAsync(id, analysisId, ct));

    /// <summary>Seçilen iyileştirmeleri kaydeder (üzerine veya kopya) ve isteğe bağlı yeniden analiz eder.</summary>
    [HttpPost("{id:guid}/improve/apply")]
    public async Task<ActionResult<ApiResponse<ImprovementApplyResultDto>>> ImproveApply(Guid id, ImprovementApplyRequest request, CancellationToken ct) =>
        OkResponse(await improvements.ApplyAsync(id, request, ct), "İyileştirmeler uygulandı.");

    /// <summary>CV danışmanı: CV ve son analiz hakkında soru-cevap.</summary>
    [HttpPost("{id:guid}/chat")]
    public async Task<ActionResult<ApiResponse<ResumeChatResponse>>> Chat(Guid id, ResumeChatRequest request, CancellationToken ct) =>
        OkResponse(await resumes.ChatAsync(id, request, ct));
}
