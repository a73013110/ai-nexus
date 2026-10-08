using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Features.Sharing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

/// <summary>The owner deletes an artifact: its shares are revoked and every revision is erased in the same transaction.</summary>
internal static class DeleteArtifact
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, ShareService shares, CancellationToken ct) =>
        {
            await HandleAsync(db, access, writes, shares, user.Id, id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteArtifact").Produces(204);

    public static async Task HandleAsync(NexusDbContext db, ResourceAccess access, ResourceWriteLock writes, ShareService shares, Guid actor, Guid id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var resource = await access.OwnerAsync(actor, id, Artifact.Kind, ct);
            resource.IsDeleted = true;
            await shares.RevokeSourceAsync(Artifact.Kind, id, ct);
            await db.Set<ArtifactRevision>().Where(x => x.ArtifactId == id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "artifact.deleted", Result = "deleted" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
}
