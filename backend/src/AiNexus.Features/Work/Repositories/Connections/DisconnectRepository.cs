using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Connections;

/// <summary>刪除已儲存的 token。已匯入的快照與 review 會保留；要再讀取其來源，必須重新連線。</summary>
internal sealed class DisconnectRepository(AppDbContext db, RepositoryWriteLock writes)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/connection", (ICurrentUser user, DisconnectRepository handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .Audited(RepositoriesAudit.ConnectionDisconnected)
        .WithName("DisconnectRepository");

    public async Task<Result> HandleAsync(Guid owner, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await db.RepositoryConnections.Where(x => x.OwnerId == owner).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
        }
        finally { writes.Gate.Release(); }
        return Result.Success;
    }
}
