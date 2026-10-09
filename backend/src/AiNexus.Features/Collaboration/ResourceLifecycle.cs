using System.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using AiNexus.Features.Persistence;
using AiNexus.Features.Operations;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Collaboration;

/// <summary>
/// Raised when a project or evaluation set is soft-deleted, inside its serializable transaction. Subscribers detach their
/// own records from the container before the save (Artifacts: a project's artifacts).
/// </summary>
public sealed record ContainerDeleted(string Kind, Guid ContainerId, Guid ActorId) : IDomainEvent;

/// <summary>Remove a container without destroying private conversations, owned files or audit evidence.</summary>
public sealed class ResourceLifecycle(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, DomainEvents events)
{
    public async Task DeleteAsync(Guid actor, Guid id, string kind, CancellationToken ct)
    {
        if (kind is not ("project" or "evaluation")) throw new InvalidOperationException("Unsupported container lifecycle.");
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var resource = await access.OwnerAsync(actor, id, kind, ct);
            // Deleted children and conversations count too: their active work blocks deletion and their links are cleared.
            var children = db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ParentId == id).Select(x => x.Id);
            if (await db.Set<BackgroundJob>().AnyAsync(x => (x.ResourceId == id || children.Contains(x.ResourceId ?? Guid.Empty)) && x.ActiveKey != null, ct))
                throw new ApiException(409, "resource_tasks_active", "此項目仍有背景任務，請先完成或取消，再刪除。");
            if (kind == "project")
            {
                if (await (from r in db.Runs join c in db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]) on r.ConversationId equals c.Id where c.ProjectId == id && r.ActiveOwnerId != null select r.Id).AnyAsync(ct))
                    throw new ApiException(409, "generation_active", "專案仍有正在生成的回答，請先完成或停止，再刪除。");
                await db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ProjectId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
                // Direct ACLs and ownership survive. Inherited project access stops with the container.
                await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ParentId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, (Guid?)null), ct);
            }
            resource.IsDeleted = true;
            resource.UpdatedAt = DateTimeOffset.UtcNow;
            events.Raise(new ContainerDeleted(kind, id, actor));
            db.AuditEvents.Add(new() { OwnerId = actor, Action = kind + ".deleted", ResourceId = id, Result = "soft_deleted" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
}
