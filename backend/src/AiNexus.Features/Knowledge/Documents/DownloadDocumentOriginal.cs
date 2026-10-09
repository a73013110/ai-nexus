using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>Streams the original file of a document the user may read, inline or as a download, through the safe file response.</summary>
internal static class DownloadDocumentOriginal
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/content", async (Guid id, bool? download, HttpContext http, ICurrentUser user, DocumentAccess documents, AttachmentService files, CancellationToken ct) =>
        {
            var original = await documents.OriginalAsync(user.Id, id, ct);
            if (!original.IsSuccess) return original.Error.ToProblem();
            var file = original.Value;
            return WebSecurity.File(http, await files.OpenAsync(file, ct), file.ContentType, file.FileName, download == true);
        })
        .WithName("DocumentOriginal");
}
