using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>
/// 刪除使用者可編輯的文件：取消其工作、清除索引與頁面；原始檔若無其他參照才釋放。
/// 檔案位元組在交易提交後才刪除。
/// </summary>
internal sealed class DeleteDocument(AppDbContext db, DocumentAccess documents, AttachmentService attachments, AttachmentWriteLock writes, AttachmentLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, DeleteDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("DeleteDocument");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var found = await documents.FindAsync(actor, id, ct, write: true);
        if (!found.IsSuccess) return found.Error;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            var current = await documents.FindAsync(actor, id, ct, write: true);
            if (!current.IsSuccess) return current.Error;
            var document = current.Value;
            document.IsDeleted = true; document.Status = "deleted"; var attachment = document.AttachmentId; document.AttachmentId = null;
            (await db.Set<Resource>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == id, ct)).IsDeleted = true;
            await db.Set<BackgroundJob>().Where(x => x.SubjectId == id && (x.Status == "running" || x.Status == "queued")).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.Set<ResourceAttachment>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            await db.Set<Chunk>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            await db.Set<DocumentPage>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "document.deleted", Result = "deleted" });
            await db.SaveChangesAsync(ct);
            if (attachment is Guid file) await lifecycle.MarkUnusedAsync([file], ct);
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
