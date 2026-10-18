using AiNexus.Data.App;
using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Attachments;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>
/// 擁有者刪除知識庫連同所有文件：進行中的工作會取消，索引、頁面與對話選取會清除，沒有其他參照的原始檔會釋放。
/// 檔案位元組在交易提交後才實際刪除，避免交易回滾時檔案已消失。
/// </summary>
internal sealed class DeleteKnowledgeCollection(AppDbContext db, ResourceAccess access, AttachmentService attachments, AttachmentWriteLock writes, AttachmentLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/collections/{id:guid}", (Guid id, ICurrentUser user, DeleteKnowledgeCollection handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DeleteKnowledgeCollection");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var owned = await access.OwnerAsync(actor, id, Collection.Kind, ct);
        if (!owned.IsSuccess) return owned.Error;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            var current = await access.OwnerAsync(actor, id, Collection.Kind, ct);
            if (!current.IsSuccess) return current.Error;
            var resource = current.Value;
            var docs = await db.Set<Document>().Where(x => x.CollectionId == id && !x.IsDeleted).ToListAsync(ct);
            var documentIds = docs.Select(x => x.Id).ToArray(); var files = docs.Where(x => x.AttachmentId != null).Select(x => x.AttachmentId!.Value).Distinct().ToArray();
            foreach (var doc in docs) { doc.IsDeleted = true; doc.AttachmentId = null; doc.Status = "deleted"; }
            resource.IsDeleted = true;
            await db.Set<Resource>().Where(x => documentIds.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
            await db.Set<BackgroundJob>().Where(x => documentIds.Contains(x.SubjectId) && (x.Status == "queued" || x.Status == "running")).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.Set<ResourceAttachment>().Where(x => documentIds.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
            await db.Set<ConversationCollection>().Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
            await db.Set<Chunk>().Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
            await db.Set<DocumentPage>().Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
            await lifecycle.MarkUnusedAsync(files, ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "knowledge.deleted", Result = "deleted" }); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
