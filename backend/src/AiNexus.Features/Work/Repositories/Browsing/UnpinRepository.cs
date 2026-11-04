using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Browsing;

/// <summary>移除釘選。不呼叫 Gitea，因此使用者已無權讀取的程式庫仍能取消釘選。</summary>
internal sealed class UnpinRepository(AppDbContext db, RepositoryService gitea) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder repository) => repository
        .MapDelete("/pin", (string owner, string name, ICurrentUser user, UnpinRepository handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, owner + "/" + name, ct))
        .Audited(RepositoriesAudit.RepositoryUnpinned)
        .WithName("UnpinRepository");

    public async Task<Result> HandleAsync(Guid owner, string repo, CancellationToken ct)
    {
        await db.RepositoryPins.Where(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl && x.Repository == repo).ExecuteDeleteAsync(ct);
        return Result.Success;
    }
}
