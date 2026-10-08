using AiNexus.Features.Identity;

namespace AiNexus.Features.Repositories;

/// <summary>Whether Gitea is enabled and the user has a usable connection on the current host.</summary>
internal static class GetRepositoryConnection
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/connection", async (ICurrentUser user, RepositoryService gitea, CancellationToken ct) => Results.Ok(await gitea.StatusAsync(user.Id, ct)))
        .WithName("GetRepositoryConnection").Produces<RepositoryStatusDto>();
}
