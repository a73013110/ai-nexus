using System.Text.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Sharing;

public sealed record SharedFilePreviewDto(AttachmentDto File, IReadOnlyList<DocumentPageDto> Pages);

/// <summary>
/// Preview and download of a file the owner explicitly included. The grant is the share's own reference, never the
/// recipient's access to the original attachment; the name shown is the one frozen in the snapshot.
/// </summary>
internal sealed class OpenSharedFile(NexusDbContext db, ShareAccess shares, AttachmentService files)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/files/{file:guid}/preview", async (Guid id, Guid file, ICurrentUser user, OpenSharedFile handler, CancellationToken ct) =>
                (await handler.PreviewAsync(user.Id, id, file, ct)).ToHttpResult())
            .WithName("PreviewSharedFile");
        routes.MapGet("/{id:guid}/files/{file:guid}", (Guid id, Guid file, bool? download, ICurrentUser user, OpenSharedFile handler, HttpContext http, CancellationToken ct) =>
            handler.OpenAsync(user.Id, id, file, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)));
    }

    public async Task<Result<(Attachment File, Stream Content)>> OpenAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        return (found.Value, await files.OpenAsync(found.Value, ct));
    }

    public async Task<Result<Attachment>> FindAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var share = await shares.RequireAsync(actor, id, ct);
        if (!share.IsSuccess) return share.Error;
        if (!share.Value.IncludeAttachments || !await db.Set<AttachmentReference>().AnyAsync(x => x.ResourceId == id && x.AttachmentId == fileId, ct)) return SharingErrors.Unavailable;
        var file = await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.StorageState == AttachmentStates.Ready, ct);
        if (file is null) return SharingErrors.Unavailable;
        var metadata = JsonSerializer.Deserialize<ShareSnapshot>(share.Value.SnapshotJson)!.Messages.SelectMany(x => x.Attachments).FirstOrDefault(x => x.Id == fileId);
        if (metadata is null) return SharingErrors.Unavailable;
        file.FileName = metadata.FileName;
        return file;
    }

    public async Task<Result<SharedFilePreviewDto>> PreviewAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        var file = found.Value;
        var documentId = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.AttachmentId == fileId && !x.IsDeleted && x.Status == "ready").OrderBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        IReadOnlyList<DocumentPageDto> pages = documentId is Guid doc
            ? await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == doc).OrderBy(x => x.PageNumber).Select(x => new DocumentPageDto(x.PageNumber, x.Text, x.Extraction, x.NeedsReview)).ToArrayAsync(ct)
            : string.IsNullOrWhiteSpace(file.ExtractedText) ? [] : [new DocumentPageDto(1, file.ExtractedText, "shared", false)];
        return new SharedFilePreviewDto(AttachmentService.Describe(file), pages);
    }
}
