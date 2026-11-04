using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using System.Text.Json;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Files.Thumbnails;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Sharing.Viewing;

/// <summary>
/// 預覽與下載擁有者明確納入分享的檔案。授權來源是分享本身的引用，絕不是收件人對原始附件的存取權；
/// 顯示的名稱是快照中凍結的那一個。
/// </summary>
internal sealed class OpenSharedFile(AppDbContext db, ShareAccess shares, AttachmentService files, AttachmentThumbnails thumbnails) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("/{id:guid}/files/{file:guid}/preview", (Guid id, Guid file, ICurrentUser user, OpenSharedFile handler, CancellationToken ct) =>
                handler.PreviewAsync(user.Id, id, file, ct))
            .Audited(SharingAudit.SharedFileRead)
            .WithName("PreviewSharedFile");
        routes.MapGet("/{id:guid}/files/{file:guid}", (Guid id, Guid file, bool? download, ICurrentUser user, OpenSharedFile handler, HttpContext http, CancellationToken ct) =>
            handler.OpenAsync(user.Id, id, file, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)))
            .Audited(SharingAudit.SharedFileRead);
        routes.MapGet("/{id:guid}/files/{file:guid}/render", (Guid id, Guid file, ICurrentUser user, OpenSharedFile handler, HttpContext http, CancellationToken ct) =>
                handler.RenderAsync(user.Id, id, file, ct).ToHttpResultAsync(x => WebSecurity.SandboxedHtml(http, x)))
            .Audited(SharingAudit.SharedFileRead)
            .WithName("RenderSharedFileHtml");
        routes.MapGet("/{id:guid}/files/{file:guid}/thumbnail", (Guid id, Guid file, ICurrentUser user, OpenSharedFile handler, HttpContext http, CancellationToken ct) =>
                handler.ThumbnailAsync(user.Id, id, file, ct).ToHttpResultAsync(x => WebSecurity.File(http, x, AttachmentThumbnails.ContentType, "thumbnail.webp", false)))
            .Audited(SharingAudit.SharedFileRead)
            .WithName("SharedFileThumbnail");
    }

    public async Task<Result<byte[]>> ThumbnailAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        var rendered = await thumbnails.RenderAsync(found.Value, token => files.OpenAsync(found.Value, token), ct);
        return rendered.IsSuccess ? rendered : SharingErrors.Unavailable;
    }

    public async Task<Result<(Attachment File, Stream Content)>> OpenAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        return (found.Value, await files.OpenAsync(found.Value, ct));
    }

    public async Task<Result<Stream>> RenderAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        if (!WebSecurity.IsHtml(found.Value.ContentType, found.Value.FileName)) return SharingErrors.Unavailable;
        return await files.OpenAsync(found.Value, ct);
    }

    public async Task<Result<Attachment>> FindAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var share = await shares.RequireAsync(actor, id, ct);
        if (!share.IsSuccess) return share.Error;
        if (!share.Value.IncludeAttachments || !await db.ResourceAttachments.AnyAsync(x => x.ResourceId == id && x.AttachmentId == fileId, ct)) return SharingErrors.Unavailable;
        var file = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.StorageState == AttachmentStates.Ready, ct);
        if (file is null) return SharingErrors.Unavailable;
        var metadata = JsonSerializer.Deserialize<ShareSnapshot>(share.Value.SnapshotJson)!.Messages.SelectMany(x => x.Attachments).FirstOrDefault(x => x.Id == fileId);
        if (metadata is null) return SharingErrors.Unavailable;
        file.FileName = metadata.FileName;
        return file;
    }

    public async Task<Result<FilePreviewDto>> PreviewAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        return await FilePreviews.ReadAsync(db, found.Value, ct);
    }
}
