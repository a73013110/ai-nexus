using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Collections;

/// <summary>
/// 擁有者刪除知識庫連同所有文件：進行中的工作會取消，索引、頁面與對話選取會清除，沒有其他參照的原始檔會釋放。
/// 檔案位元組在交易提交後才實際刪除，避免交易回滾時檔案已消失。
/// </summary>
internal sealed class DeleteKnowledgeCollection(AppDbContext db, ResourceAccess access, DocumentWriteLock writes, AttachmentLifecycle lifecycle) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/collections/{id:guid}", (Guid id, ICurrentUser user, DeleteKnowledgeCollection handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(KnowledgeAudit.CollectionDeleted)
        .WithName("DeleteKnowledgeCollection");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await writes.TryAcquireForCollectionAsync(id, ct)) return CollaborationErrors.ResourceBusy;
            var current = await access.OwnerAsync(actor, id, Collection.Kind, ct);
            if (!current.IsSuccess) return current.Error;
            var resource = current.Value;
            var docs = await db.Documents.Where(x => x.CollectionId == id && !x.IsDeleted).ToListAsync(ct);
            var documentIds = docs.Select(x => x.Id).ToArray(); var files = docs.Where(x => x.AttachmentId != null).Select(x => x.AttachmentId!.Value).Distinct().ToArray();
            await lifecycle.LockOwnersAsync(files, ct);
            foreach (var doc in docs) { doc.IsDeleted = true; doc.AttachmentId = null; doc.Status = "deleted"; }
            resource.IsDeleted = true;
            await db.Resources.Where(x => documentIds.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
            await db.BackgroundJobs.Where(x => documentIds.Contains(x.SubjectId) && (x.Status == RunStates.Queued || x.Status == RunStates.Running)).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.ResourceAttachments.Where(x => documentIds.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
            await db.ConversationCollections.Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
            await db.Chunks.Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
            await db.DocumentPages.Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
            await lifecycle.MarkUnusedAsync(files, ct);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
