using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Connections;

/// <summary>刪除已儲存的 token。已匯入的快照與 review 會保留；要再讀取其來源，必須重新連線。</summary>
internal sealed class DisconnectRepository(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/connection", (ICurrentUser user, DisconnectRepository handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .Audited(RepositoriesAudit.ConnectionDisconnected)
        .WithName("DisconnectRepository");

    public async Task<Result> HandleAsync(Guid owner, CancellationToken ct)
    {
        // 與連線排隊：連線讀到舊列後才更新時，列不能在中途消失。
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockUserRowAsync(owner, ct);
        await db.RepositoryConnections.Where(x => x.OwnerId == owner).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success;
    }
}
