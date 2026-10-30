using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Work.Conversations;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat.Transcripts;

/// <summary>
/// 管理員稽核檢視中預覽與下載對話的附件。授權來源是「這個附件掛在這段對話的訊息上」加上
/// <see cref="FeatureCatalog.IdentityUsers"/>，不是對原始附件的擁有權；每次開啟都稽核，主體是對話擁有者。
/// 路由形狀與分享的檔案相同（預覽、原檔、HTML 沙箱），前端閱讀器以同一套範圍處理兩者。
/// </summary>
internal sealed class OpenAdminConversationFile(AppDbContext db, AccessService access, AuditTrail audit, AttachmentService files)
{
    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("/conversations/{id:guid}/files/{file:guid}/preview", async (Guid id, Guid file, ICurrentUser user, OpenAdminConversationFile handler, CancellationToken ct) =>
                (await handler.PreviewAsync(user.Id, id, file, ct)).ToHttpResult())
            .Audited(ConversationsAudit.ConversationFileRead)
            .WithName("PreviewAdminConversationFile");
        routes.MapGet("/conversations/{id:guid}/files/{file:guid}", (Guid id, Guid file, bool? download, ICurrentUser user, OpenAdminConversationFile handler, HttpContext http, CancellationToken ct) =>
                handler.OpenAsync(user.Id, id, file, ct).ToHttpResultAsync(x => WebSecurity.File(http, x.Content, x.File.ContentType, x.File.FileName, download == true)))
            .Audited(ConversationsAudit.ConversationFileRead)
            .WithName("OpenAdminConversationFile");
        routes.MapGet("/conversations/{id:guid}/files/{file:guid}/render", (Guid id, Guid file, ICurrentUser user, OpenAdminConversationFile handler, HttpContext http, CancellationToken ct) =>
                handler.RenderAsync(user.Id, id, file, ct).ToHttpResultAsync(x => WebSecurity.SandboxedHtml(http, x)))
            .Audited(ConversationsAudit.ConversationFileRead)
            .WithName("RenderAdminConversationFile");
    }

    public async Task<Result<FilePreviewDto>> PreviewAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        var found = await FindAsync(actor, id, fileId, ct);
        if (!found.IsSuccess) return found.Error;
        return await FilePreviews.ReadAsync(db, found.Value, ct);
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
        if (!WebSecurity.IsHtml(found.Value.ContentType, found.Value.FileName)) return ConversationsErrors.AdminResourceNotFound;
        return await files.OpenAsync(found.Value, ct);
    }

    /// <summary>對話（含已軟刪除）裡任一則訊息引用的、仍可讀的附件；不在這段對話裡一律回 404，不透露檔案是否存在。</summary>
    private async Task<Result<Attachment>> FindAsync(Guid actor, Guid id, Guid fileId, CancellationToken ct)
    {
        if (!await access.HoldsAsync(actor, FeatureCatalog.IdentityUsers, ct)) return AccessControlErrors.FeatureForbidden;
        var owner = await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).AsNoTracking()
            .Where(x => x.Id == id).Select(x => (Guid?)x.OwnerId).SingleOrDefaultAsync(ct);
        if (owner is null) return ConversationsErrors.AdminResourceNotFound;
        var linked = await db.Set<MessageAttachment>().AnyAsync(x => x.AttachmentId == fileId
            && db.Messages.Any(m => m.Id == x.MessageId && m.ConversationId == id), ct);
        var file = linked
            ? await db.Set<Attachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.StorageState == AttachmentStates.Ready, ct)
            : null;
        if (file is null) return ConversationsErrors.AdminResourceNotFound;
        audit.Describe(subject: owner.Value, details: new { file = fileId });
        return file;
    }
}
