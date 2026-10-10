using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories;

/// <summary>Deletes the stored token. Imported snapshots and reviews stay; reading their source needs a new connection.</summary>
internal sealed class DisconnectRepository(NexusDbContext db, RepositoryWriteLock writes)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/connection", async (ICurrentUser user, DisconnectRepository handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(user.Id, ct);
            return TypedResults.NoContent();
        })
        .WithName("DisconnectRepository");

    public async Task HandleAsync(Guid owner, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await db.Set<RepositoryConnection>().Where(x => x.OwnerId == owner).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = owner, Action = "repository.disconnected", Result = "deleted" });
            await db.SaveChangesAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
}
