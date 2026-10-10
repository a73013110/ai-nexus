using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>
/// The owner deletes a collection with all of its documents: running jobs are cancelled, the index, pages and
/// conversation selections are erased and originals nothing else holds are released. Bytes are deleted after commit.
/// </summary>
internal sealed class DeleteKnowledgeCollection(NexusDbContext db, ResourceAccess access, AttachmentService attachments, AttachmentWriteLock writes, AttachmentLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/collections/{id:guid}", (Guid id, ICurrentUser user, DeleteKnowledgeCollection handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DeleteKnowledgeCollection");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var owned = await access.OwnerAsync(actor, id, KnowledgeCollection.Kind, ct);
        if (!owned.IsSuccess) return owned.Error;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            var current = await access.OwnerAsync(actor, id, KnowledgeCollection.Kind, ct);
            if (!current.IsSuccess) return current.Error;
            var resource = current.Value;
            var docs = await db.Set<KnowledgeDocument>().Where(x => x.CollectionId == id && !x.IsDeleted).ToListAsync(ct);
            var documentIds = docs.Select(x => x.Id).ToArray(); var files = docs.Where(x => x.AttachmentId != null).Select(x => x.AttachmentId!.Value).Distinct().ToArray();
            foreach (var doc in docs) { doc.IsDeleted = true; doc.AttachmentId = null; doc.Status = "deleted"; }
            resource.IsDeleted = true;
            await db.Set<WorkspaceResource>().Where(x => documentIds.Contains(x.Id)).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDeleted, true), ct);
            await db.Set<BackgroundJob>().Where(x => documentIds.Contains(x.SubjectId) && (x.Status == "queued" || x.Status == "running")).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.Set<AttachmentReference>().Where(x => documentIds.Contains(x.ResourceId)).ExecuteDeleteAsync(ct);
            await db.Set<ConversationKnowledge>().Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
            await db.Set<KnowledgeChunk>().Where(x => documentIds.Contains(x.DocumentId)).ExecuteDeleteAsync(ct);
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
