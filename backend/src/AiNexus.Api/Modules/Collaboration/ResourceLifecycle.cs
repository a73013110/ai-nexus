using System.Data;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Artifacts;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Collaboration;

/// <summary>Remove a container without destroying private conversations, owned files or audit evidence.</summary>
public sealed class ResourceLifecycle(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes)
{
    public async Task DeleteAsync(Guid actor, Guid id, string kind, CancellationToken ct)
    {
        if (kind is not ("project" or "evaluation")) throw new InvalidOperationException("Unsupported container lifecycle.");
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var resource = await access.OwnerAsync(actor, id, kind, ct);
            var children = db.Set<WorkspaceResource>().Where(x => x.ParentId == id).Select(x => x.Id);
            if (await db.Set<BackgroundJob>().AnyAsync(x => (x.ResourceId == id || children.Contains(x.ResourceId ?? Guid.Empty)) && x.ActiveKey != null, ct))
                throw new ApiException(409, "resource_tasks_active", "此項目仍有背景任務，請先完成或取消，再刪除。");
            if (kind == "project")
            {
                if (await (from r in db.Runs join c in db.Conversations on r.ConversationId equals c.Id where c.ProjectId == id && r.ActiveOwnerId != null select r.Id).AnyAsync(ct))
                    throw new ApiException(409, "generation_active", "專案仍有正在生成的回答，請先完成或停止，再刪除。");
                await db.Conversations.Where(x => x.ProjectId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
                await db.Set<Artifact>().Where(x => x.ProjectId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
                // Direct ACLs and ownership survive. Inherited project access stops with the container.
                await db.Set<WorkspaceResource>().Where(x => x.ParentId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, (Guid?)null), ct);
            }
            resource.IsDeleted = true;
            resource.UpdatedAt = DateTimeOffset.UtcNow;
            db.AuditEvents.Add(new() { OwnerId = actor, Action = kind + ".deleted", ResourceId = id, Result = "soft_deleted" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
}
