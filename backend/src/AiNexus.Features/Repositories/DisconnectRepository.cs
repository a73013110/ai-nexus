using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories;

/// <summary>Deletes the stored token. Imported snapshots and reviews stay; reading their source needs a new connection.</summary>
internal static class DisconnectRepository
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/connection", HandleAsync)
        .WithName("DisconnectRepository");

    private static async Task<IResult> HandleAsync(ICurrentUser user, NexusDbContext db, RepositoryWriteLock writes, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await db.Set<RepositoryConnection>().Where(x => x.OwnerId == user.Id).ExecuteDeleteAsync(ct);
            db.AuditEvents.Add(new AuditEvent { OwnerId = user.Id, ResourceId = user.Id, Action = "repository.disconnected", Result = "deleted" });
            await db.SaveChangesAsync(ct);
        }
        finally { writes.Gate.Release(); }
        return Results.NoContent();
    }
}
