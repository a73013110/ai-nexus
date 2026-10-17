using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Connections;

/// <summary>刪除已儲存的 token。已匯入的快照與 review 會保留；要再讀取其來源，必須重新連線。</summary>
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
