using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Attachments.Files;

/// <summary>
/// 每個請求一個 multipart 檔案。大小、名稱、內容偵測、配額預約與儲存寫入都由 <see cref="AttachmentService.UploadAsync"/> 完成，
/// 與其他模組上傳走同一條路徑。
/// </summary>
internal sealed class UploadAttachment(AttachmentService files)
{
    // 應用程式的標頭式防偽造（antiforgery）中介軟體同樣會驗證此上傳。
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", (HttpRequest request, ICurrentUser user, UploadAttachment handler, CancellationToken ct) => handler.HandleAsync(user.Id, request, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(AttachmentsModule.UploadBodyLimit).RequireRateLimiting(AttachmentsModule.UploadRateLimit).WithName("UploadAttachment");

    public async Task<Result<AttachmentDto>> HandleAsync(Guid owner, HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType) return AttachmentsErrors.MultipartRequired;
        var form = await request.ReadFormAsync(ct);
        if (form.Files.Count != 1) return AttachmentsErrors.FileRequired;
        return await files.UploadAsync(owner, form.Files[0], ct);
    }
}
