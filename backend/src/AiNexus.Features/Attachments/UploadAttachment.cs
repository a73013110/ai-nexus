using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Attachments;

/// <summary>
/// One multipart file per request. Size, name, content sniffing, quota reservation and the storage write are all done
/// by <see cref="AttachmentService.UploadAsync"/>, the same path other modules upload through.
/// </summary>
internal sealed class UploadAttachment(AttachmentService files)
{
    // The application's header-based antiforgery middleware validates this upload as well.
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
