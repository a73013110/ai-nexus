using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Repositories.Connections;

/// <summary>回傳 Gitea 是否啟用，以及使用者在目前主機上是否已有可用的連線。</summary>
internal sealed class GetRepositoryConnection(RepositoryService gitea)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/connection", async (ICurrentUser user, GetRepositoryConnection handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .NotAudited("使用者自己的連結狀態")
        .WithName("GetRepositoryConnection");

    public Task<RepositoryStatusDto> HandleAsync(Guid owner, CancellationToken ct) => gitea.StatusAsync(owner, ct);
}
