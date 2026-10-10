using AiNexus.Features.Identity;

namespace AiNexus.Features.Repositories;

/// <summary>Whether Gitea is enabled and the user has a usable connection on the current host.</summary>
internal sealed class GetRepositoryConnection(RepositoryService gitea)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/connection", async (ICurrentUser user, GetRepositoryConnection handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("GetRepositoryConnection");

    public Task<RepositoryStatusDto> HandleAsync(Guid owner, CancellationToken ct) => gitea.StatusAsync(owner, ct);
}
