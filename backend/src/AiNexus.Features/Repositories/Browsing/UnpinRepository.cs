using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Browsing;

/// <summary>Removes a pin; no Gitea call, so a repository the user can no longer read can still be unpinned.</summary>
internal sealed class UnpinRepository(NexusDbContext db, RepositoryService gitea)
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
