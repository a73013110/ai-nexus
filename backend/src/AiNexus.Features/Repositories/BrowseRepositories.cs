using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Repositories;

/// <summary>Read-only Gitea browsing with the user's own token: commits, repositories, folders, files and open issues.</summary>
internal sealed class BrowseRepositories(RepositoryService gitea)
{
    /// <summary>Mapped separately because the commit list comes first in the published endpoint order.</summary>
    public static RouteHandlerBuilder MapCommits(RouteGroupBuilder routes) => routes
        .MapGet("/commits", (string repository, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
            handler.CommitsAsync(user.Id, repository, ct).ToHttpResultAsync())
        .WithName("ListRepositoryCommits");

    public static void Map(RouteGroupBuilder routes)
    {
        routes.MapGet("", (int? page, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
                handler.ListAsync(user.Id, page ?? 1, ct).ToHttpResultAsync())
            .WithName("ListRepositories");
        routes.MapGet("/tree", (string repository, string? commit, string? path, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
                handler.TreeAsync(user.Id, repository, commit, path, ct).ToHttpResultAsync())
            .WithName("ReadRepositoryTree");
        routes.MapGet("/file", (string repository, string commit, string path, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
                handler.FileAsync(user.Id, repository, commit, path, ct).ToHttpResultAsync())
            .WithName("ReadRepositoryFile");
        routes.MapGet("/issues", (string repository, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
                handler.IssuesAsync(user.Id, repository, ct).ToHttpResultAsync())
            .WithName("ListRepositoryIssues");
    }

    public Task<Result<IReadOnlyList<RepositoryCommitDto>>> CommitsAsync(Guid owner, string repository, CancellationToken ct) => gitea.CommitsAsync(owner, repository, ct);

    public Task<Result<RepositoryPageDto>> ListAsync(Guid owner, int page, CancellationToken ct) => gitea.ListAsync(owner, page, ct);

    public Task<Result<RepositoryTreeDto>> TreeAsync(Guid owner, string repository, string? commit, string? path, CancellationToken ct) => gitea.TreeAsync(owner, repository, commit, path, ct);

    public Task<Result<RepositoryFileDto>> FileAsync(Guid owner, string repository, string commit, string path, CancellationToken ct) => gitea.FileAsync(owner, repository, commit, path, ct);

    public Task<Result<IReadOnlyList<RepositoryIssueDto>>> IssuesAsync(Guid owner, string repository, CancellationToken ct) => gitea.IssuesAsync(owner, repository, ct);
}
