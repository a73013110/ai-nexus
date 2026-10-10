using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Artifacts;

/// <summary>Raised in the deleting transaction; subscribers (Sharing revokes the artifact's shares) run before it is saved.</summary>
public sealed record ArtifactDeleted(Guid ArtifactId, Guid OwnerId) : IDomainEvent;

/// <summary>The owner deletes an artifact: its shares are revoked and every revision is erased in the same transaction.</summary>
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
