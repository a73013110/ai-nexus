using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>Streams the original file of a document the user may read, inline or as a download, through the safe file response.</summary>
internal sealed class DownloadDocumentOriginal(DocumentAccess documents, AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/content", (Guid id, bool? download, HttpContext http, ICurrentUser user, DownloadDocumentOriginal handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)))
        .WithName("DocumentOriginal");

    public async Task<Result<(Attachment File, Stream Content)>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var original = await documents.OriginalAsync(actor, id, ct);
        if (!original.IsSuccess) return original.Error;
        return (original.Value, await files.OpenAsync(original.Value, ct));
    }
}
