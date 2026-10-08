using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Attachments;

/// <summary>Streams one of the caller's attachments, inline or as a download, through the safe file response.</summary>
internal static class DownloadAttachment
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/content", async (Guid id, bool? download, HttpContext http, ICurrentUser user, AttachmentService files, CancellationToken ct) =>
        {
            var found = await files.FindOwnedAsync(user.Id, id, ct);
            if (!found.IsSuccess) return found.Error.ToProblem();
            var file = found.Value;
            return WebSecurity.File(http, await files.OpenAsync(file, ct), file.ContentType, file.FileName, download == true);
        })
        .WithName("DownloadAttachment");
}
