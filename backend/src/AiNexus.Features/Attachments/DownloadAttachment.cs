using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Attachments;

/// <summary>Streams one of the caller's attachments, inline or as a download, through the safe file response.</summary>
internal sealed class DownloadAttachment(AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/content", (Guid id, bool? download, HttpContext http, ICurrentUser user, DownloadAttachment handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)))
        .WithName("DownloadAttachment");

    public async Task<Result<(Attachment File, Stream Content)>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var file = await files.OwnedAsync(owner, id, ct);
        if (!file.IsSuccess) return file.Error;
        return (file.Value, await files.OpenAsync(file.Value, ct));
    }
}
