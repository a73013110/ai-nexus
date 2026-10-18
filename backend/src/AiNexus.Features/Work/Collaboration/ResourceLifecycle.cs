using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Jobs;
using System.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Collaboration;

/// <summary>
/// 專案或評估集被邏輯刪除時發出。位於可序列化交易內，且所有 <see cref="IContainerDeletionCheck"/> 都已放行之後。
/// 訂閱者要在儲存前先解除自己的紀錄與該容器的關聯（對話模組解除專案下的對話，成果模組解除專案下的成果）。
/// </summary>
public sealed record ContainerDeleted(string Kind, Guid ContainerId, Guid ActorId) : IDomainEvent;

/// <summary>刪除容器，但保留私人對話、擁有的檔案與稽核證據；只做邏輯刪除。</summary>
public sealed class ResourceLifecycle(AppDbContext db, ResourceAccess access, ResourceWriteLock writes, DomainEvents events, IEnumerable<IContainerDeletionCheck> checks, TimeProvider clock)
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
            // 已邏輯刪除的子資源也要納入檢查（查詢需忽略軟刪除過濾器）：它們仍在進行中的工作會阻擋刪除。
            var children = db.Set<Resource>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ParentId == id).Select(x => x.Id);
            if (await db.Set<BackgroundJob>().AnyAsync(x => (x.ResourceId == id || children.Contains(x.ResourceId ?? Guid.Empty)) && x.ActiveKey != null, ct))
                return CollaborationErrors.TasksActive;
            foreach (var check in checks)
                if (await check.CheckAsync(kind, id, ct) is { IsSuccess: false } refused) return refused.Error;
            if (kind == "project")
            {
                // 子資源的直接分享與擁有者保持不變；只有靠專案繼承的存取會隨專案失效，所以要清空它們的 ParentId。
                await db.Set<Resource>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ParentId == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, (Guid?)null), ct);
            }
            resource.IsDeleted = true;
            resource.UpdatedAt = clock.GetUtcNow();
            events.Raise(new ContainerDeleted(kind, id, actor));
            db.AuditEvents.Add(new() { OwnerId = actor, Action = kind + ".deleted", ResourceId = id, Result = "soft_deleted" });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return Result.Success;
    }
}
