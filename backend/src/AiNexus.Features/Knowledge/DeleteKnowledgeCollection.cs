using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// The owner deletes a collection with all of its documents: running jobs are cancelled, the index, pages and
/// conversation selections are erased and originals nothing else holds are released. Bytes are deleted after commit.
/// </summary>
internal sealed class DeleteKnowledgeCollection(NexusDbContext db, ResourceAccess access, AttachmentService attachments, AttachmentWriteLock writes, AttachmentLifecycle lifecycle)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/collections/{id:guid}", async (Guid id, ICurrentUser user, DeleteKnowledgeCollection handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(user.Id, id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteKnowledgeCollection").Produces(204);

    public async Task HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await access.OwnerAsync(actor, id, KnowledgeCollection.Kind, ct); await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await attachments.LockOwnerAsync(actor, ct);
            var resource = await access.OwnerAsync(actor, id, KnowledgeCollection.Kind, ct);
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
    }
}
