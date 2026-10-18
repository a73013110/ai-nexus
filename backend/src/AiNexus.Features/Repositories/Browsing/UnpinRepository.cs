using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Browsing;

/// <summary>移除釘選。不呼叫 Gitea，因此使用者已無權讀取的程式庫仍能取消釘選。</summary>
internal sealed class UnpinRepository(AppDbContext db, RepositoryService gitea)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder repository) => repository
        .MapDelete("/pin", async (string owner, string name, ICurrentUser user, UnpinRepository handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(user.Id, owner + "/" + name, ct);
            return TypedResults.NoContent();
        })
        .WithName("UnpinRepository");

    public Task HandleAsync(Guid owner, string repo, CancellationToken ct) =>
        db.Set<RepositoryPin>().Where(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl && x.Repository == repo).ExecuteDeleteAsync(ct);
}
