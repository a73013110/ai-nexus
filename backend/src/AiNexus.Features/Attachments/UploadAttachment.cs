using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Attachments;

/// <summary>
/// One multipart file per request. Size, name, content sniffing, quota reservation and the storage write are all done
/// by <see cref="AttachmentService.UploadAsync"/>, the same path other modules upload through.
/// </summary>
internal static class UploadAttachment
{
    // The application's header-based antiforgery middleware validates this upload as well.
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("", async (HttpContext http, ICurrentUser user, AttachmentService files, CancellationToken ct) =>
        {
            if (!http.Request.HasFormContentType) return AttachmentsErrors.MultipartRequired.ToProblem();
            var form = await http.Request.ReadFormAsync(ct);
            if (form.Files.Count != 1) return AttachmentsErrors.FileRequired.ToProblem();
            return Results.Ok(await files.UploadAsync(user.Id, form.Files[0], ct));
        })
        .WithRequestBodyLimit(AttachmentsModule.UploadBodyLimit).RequireRateLimiting(AttachmentsModule.UploadRateLimit).WithName("UploadAttachment").Produces<AttachmentDto>();
}
