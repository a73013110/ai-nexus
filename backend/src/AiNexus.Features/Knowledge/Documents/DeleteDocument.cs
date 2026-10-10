using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// Deletes a document the user may edit: its jobs are cancelled, its index and pages erased and its original released
/// when nothing else holds it. Bytes are deleted after commit.
/// </summary>
internal sealed class DeleteDocument(NexusDbContext db, DocumentAccess documents, AttachmentService attachments, AttachmentWriteLock writes, AttachmentLifecycle lifecycle)
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
            (await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == id, ct)).IsDeleted = true;
            await db.Set<BackgroundJob>().Where(x => x.SubjectId == id && (x.Status == "running" || x.Status == "queued")).ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true), ct);
            await db.Set<AttachmentReference>().Where(x => x.ResourceId == id).ExecuteDeleteAsync(ct);
            await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
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
