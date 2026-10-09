using System.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Collaboration;

/// <summary>
/// Raised when a project or evaluation set is soft-deleted, inside its serializable transaction, after every
/// <see cref="IContainerDeletionCheck"/> allowed it. Subscribers detach their own records from the container before the
/// save (Conversations: a project's conversations; Artifacts: a project's artifacts).
/// </summary>
public sealed record ContainerDeleted(string Kind, Guid ContainerId, Guid ActorId) : IDomainEvent;

/// <summary>Remove a container without destroying private conversations, owned files or audit evidence.</summary>
public sealed class ResourceLifecycle(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, DomainEvents events, IEnumerable<IContainerDeletionCheck> checks)
{
    public async Task<Result> DeleteAsync(Guid actor, Guid id, string kind, CancellationToken ct)
    {
        if (kind is not ("project" or "evaluation")) throw new InvalidOperationException("Unsupported container lifecycle.");
        using (await writes.AcquireAsync(id, ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var owned = await access.OwnerAsync(actor, id, kind, ct);
            if (!owned.IsSuccess) return owned.Error;
            var resource = owned.Value;
            // Deleted children count too: their active work blocks deletion and their links are cleared.
            var children = db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ParentId == id).Select(x => x.Id);
            if (await db.Set<BackgroundJob>().AnyAsync(x => (x.ResourceId == id || children.Contains(x.ResourceId ?? Guid.Empty)) && x.ActiveKey != null, ct))
                return CollaborationErrors.TasksActive;
            foreach (var check in checks)
                if (await check.CheckAsync(kind, id, ct) is { IsSuccess: false } refused) return refused.Error;
            if (kind == "project")
            {
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
        return Result.Success;
    }
}
