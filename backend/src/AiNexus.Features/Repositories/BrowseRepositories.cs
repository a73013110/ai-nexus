using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Repositories;

/// <summary>Read-only Gitea browsing with the user's own token: commits, repositories, folders, files and open issues.</summary>
internal static class BrowseRepositories
{
    /// <summary>Mapped separately because the commit list comes first in the published endpoint order.</summary>
    public static RouteHandlerBuilder MapCommits(RouteGroupBuilder routes) => routes
        .MapGet("/commits", async (string repository, ICurrentUser user, RepositoryService gitea, CancellationToken ct) =>
            (await gitea.CommitsAsync(user.Id, repository, ct)).ToHttpResult())
        .WithName("ListRepositoryCommits").Produces<IReadOnlyList<RepositoryCommitDto>>();

    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("", async (int? page, ICurrentUser user, RepositoryService gitea, CancellationToken ct) =>
                (await gitea.ListAsync(user.Id, page ?? 1, ct)).ToHttpResult())
            .WithName("ListRepositories").Produces<RepositoryPageDto>();
        routes.MapGet("/tree", async (string repository, string? commit, string? path, ICurrentUser user, RepositoryService gitea, CancellationToken ct) =>
                (await gitea.TreeAsync(user.Id, repository, commit, path, ct)).ToHttpResult())
            .WithName("ReadRepositoryTree").Produces<RepositoryTreeDto>();
        routes.MapGet("/file", async (string repository, string commit, string path, ICurrentUser user, RepositoryService gitea, CancellationToken ct) =>
                (await gitea.FileAsync(user.Id, repository, commit, path, ct)).ToHttpResult())
            .WithName("ReadRepositoryFile").Produces<RepositoryFileDto>();
        routes.MapGet("/issues", async (string repository, ICurrentUser user, RepositoryService gitea, CancellationToken ct) =>
                (await gitea.IssuesAsync(user.Id, repository, ct)).ToHttpResult())
            .WithName("ListRepositoryIssues").Produces<IReadOnlyList<RepositoryIssueDto>>();
    }
}
