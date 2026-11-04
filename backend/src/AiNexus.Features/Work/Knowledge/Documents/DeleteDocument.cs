using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>
/// 刪除使用者可編輯的文件：取消其工作、清除索引與頁面；原始檔若無其他參照才釋放。
/// 檔案位元組在交易提交後才刪除。
/// </summary>
internal sealed class DeleteDocument(AppDbContext db, DocumentAccess documents, DocumentWriteLock writes, AttachmentLifecycle lifecycle) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteDocument handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(KnowledgeAudit.DocumentDeleted)
        .WithName("DeleteDocument");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await writes.TryAcquireForDocumentAsync(id, ct)) return CollaborationErrors.ResourceBusy;
            var current = await documents.FindAsync(actor, id, ct, write: true);
            if (!current.IsSuccess) return current.Error;
            var document = current.Value;
            var attachment = document.AttachmentId;
            if (attachment is Guid owned) await lifecycle.LockOwnersAsync([owned], ct);
            document.IsDeleted = true; document.Status = "deleted"; document.AttachmentId = null;
            (await db.Resources.IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == id, ct)).IsDeleted = true;
            await db.BackgroundJobs.Where(x => x.SubjectId == id && (x.Status == RunStates.Queued || x.Status == RunStates.Running)).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.ResourceAttachments.Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            await db.Chunks.Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            await db.DocumentPages.Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
            if (attachment is Guid file) await lifecycle.MarkUnusedAsync([file], ct);
            await transaction.CommitAsync(ct);
        }
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
