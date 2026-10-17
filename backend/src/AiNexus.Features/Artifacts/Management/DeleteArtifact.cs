using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts.Management;

/// <summary>在刪除交易中發出；訂閱者（分享模組會在此撤銷成果的分享）於儲存前執行。</summary>
public sealed record ArtifactDeleted(Guid ArtifactId, Guid OwnerId) : IDomainEvent;

/// <summary>擁有者刪除成果：成果本身只標記為軟刪除，所有版本則實際清除，兩者與分享撤銷（由事件處理）在同一交易中完成。</summary>
internal sealed class DeleteArtifact(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, DomainEvents events)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeleteArtifact handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DeleteArtifact");

    public async Task<Result> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        using (await writes.AcquireAsync(id, ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var resource = await access.OwnerAsync(actor, id, Artifact.Kind, ct);
            if (!resource.IsSuccess) return resource.Error;
            resource.Value.IsDeleted = true;
            events.Raise(new ArtifactDeleted(id, actor));
            await db.Set<ArtifactRevision>().Where(x => x.ArtifactId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "artifact.deleted", Result = "deleted" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Result.Success;
        }
    }
}
