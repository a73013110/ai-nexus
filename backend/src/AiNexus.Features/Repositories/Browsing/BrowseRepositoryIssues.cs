using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Repositories.Browsing;

public sealed record RepositoryCommentDto(long Id, string Author, string Body, DateTimeOffset? CreatedAt, string Url);
public sealed record RepositoryIssueDto(int Number, string Title, string State, string Author, IReadOnlyList<RepositoryLabelDto> Labels, IReadOnlyList<string> Assignees,
    int Comments, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, string Url);
public sealed record RepositoryIssuePageDto(IReadOnlyList<RepositoryIssueDto> Items, int Page, bool HasMore);
public sealed record RepositoryIssueDetailDto(RepositoryIssueDto Issue, string Body, IReadOnlyList<RepositoryCommentDto> Comments);
public sealed record RepositoryPullDto(int Number, string Title, string State, bool Merged, bool Draft, string Author, string HeadRef, string BaseRef,
    IReadOnlyList<RepositoryLabelDto> Labels, int Comments, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, string Url);
public sealed record RepositoryPullPageDto(IReadOnlyList<RepositoryPullDto> Items, int Page, bool HasMore);
/// <summary>A pull request with the commits a review compares: its head and the merge base with the target branch.</summary>
public sealed record RepositoryPullDetailDto(RepositoryPullDto Pull, string Body, string HeadCommit, string BaseCommit, IReadOnlyList<RepositoryCommentDto> Comments);

/// <summary>Issues and pull requests, read-only, with their conversations.</summary>
internal sealed class BrowseRepositoryIssues(RepositoryService gitea)
{
    private const int PageSize = 20;
    private const int MaxComments = 100;

    public static void Map(RouteGroupBuilder repository)
    {
        repository.MapGet("/issues", (string owner, string name, string? state, string? q, int? page, ICurrentUser user, BrowseRepositoryIssues handler, CancellationToken ct) =>
                handler.IssuesAsync(user.Id, owner + "/" + name, state ?? "open", q, page ?? 1, ct).ToHttpResultAsync())
            .WithName("ListRepositoryIssues");
        repository.MapGet("/issues/{number:int}", (string owner, string name, int number, ICurrentUser user, BrowseRepositoryIssues handler, CancellationToken ct) =>
                handler.IssueAsync(user.Id, owner + "/" + name, number, ct).ToHttpResultAsync())
            .WithName("ReadRepositoryIssue");
        repository.MapGet("/pulls", (string owner, string name, string? state, int? page, ICurrentUser user, BrowseRepositoryIssues handler, CancellationToken ct) =>
                handler.PullsAsync(user.Id, owner + "/" + name, state ?? "open", page ?? 1, ct).ToHttpResultAsync())
            .WithName("ListRepositoryPulls");
        repository.MapGet("/pulls/{number:int}", (string owner, string name, int number, ICurrentUser user, BrowseRepositoryIssues handler, CancellationToken ct) =>
                handler.PullAsync(user.Id, owner + "/" + name, number, ct).ToHttpResultAsync())
            .WithName("ReadRepositoryPull");
    }

    public async Task<Result<RepositoryIssuePageDto>> IssuesAsync(Guid owner, string repo, string state, string? query, int page, CancellationToken ct)
    {
        if (Check(repo, state, page) is { } invalid) return invalid;
        var q = (query ?? "").Trim();
        if (q.Length > 100 || q.Any(char.IsControl)) return RepositoriesErrors.InvalidState;
        var read = await gitea.GetListAsync(owner, RepositoryService.Route(repo) + $"/issues?state={state}&type=issues&page={page}&limit={PageSize}" +
            (q.Length > 0 ? "&q=" + Uri.EscapeDataString(q) : ""), ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        var rows = json.RootElement.EnumerateArray().Take(PageSize).ToArray();
        return new RepositoryIssuePageDto(rows.Select(x => Issue(repo, x)).Where(x => x.Number > 0).ToArray(), page, rows.Length == PageSize);
    }

    public async Task<Result<RepositoryIssueDetailDto>> IssueAsync(Guid owner, string repo, int number, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (number < 1) return RepositoriesErrors.NotFound;
        var read = await gitea.GetAsync(owner, RepositoryService.Route(repo) + "/issues/" + number, ct);
        if (!read.IsSuccess) return read.Error!;
        RepositoryIssueDto issue; string body;
        using (var json = read.Value!)
        {
            // Gitea serves pull requests through the issue endpoint too; they have their own page.
            if (json.RootElement.TryGetProperty("pull_request", out var pull) && pull.ValueKind == JsonValueKind.Object) return RepositoriesErrors.NotFound;
            issue = Issue(repo, json.RootElement); body = GiteaJson.Text(json.RootElement, "body", 32000);
        }
        var comments = await CommentsAsync(owner, repo, number, "issues", ct);
        return comments.IsSuccess ? new RepositoryIssueDetailDto(issue, body, comments.Value!) : comments.Error!;
    }

    public async Task<Result<RepositoryPullPageDto>> PullsAsync(Guid owner, string repo, string state, int page, CancellationToken ct)
    {
        if (Check(repo, state, page) is { } invalid) return invalid;
        var read = await gitea.GetListAsync(owner, RepositoryService.Route(repo) + $"/pulls?state={state}&sort=recentupdate&page={page}&limit={PageSize}", ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        var rows = json.RootElement.EnumerateArray().Take(PageSize).ToArray();
        return new RepositoryPullPageDto(rows.Select(x => Pull(repo, x)).Where(x => x.Number > 0).ToArray(), page, rows.Length == PageSize);
    }

    public async Task<Result<RepositoryPullDetailDto>> PullAsync(Guid owner, string repo, int number, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (number < 1) return RepositoriesErrors.NotFound;
        var read = await gitea.GetAsync(owner, RepositoryService.Route(repo) + "/pulls/" + number, ct);
        if (!read.IsSuccess) return read.Error!;
        RepositoryPullDto pull; string body, head, basis;
        using (var json = read.Value!)
        {
            var root = json.RootElement;
            pull = Pull(repo, root); body = GiteaJson.Text(root, "body", 32000);
            head = GiteaJson.Text(GiteaJson.Child(root, "head"), "sha", 64).ToLowerInvariant();
            basis = GiteaJson.Text(root, "merge_base", 64).ToLowerInvariant();
            if (!RepositoryService.IsCommit(basis)) basis = GiteaJson.Text(GiteaJson.Child(root, "base"), "sha", 64).ToLowerInvariant();
        }
        if (!RepositoryService.IsCommit(head) || !RepositoryService.IsCommit(basis)) throw new ExternalServiceException(RepositoriesErrors.ResponseInvalid);
        var comments = await CommentsAsync(owner, repo, number, "pulls", ct);
        return comments.IsSuccess ? new RepositoryPullDetailDto(pull, body, head, basis, comments.Value!) : comments.Error!;
    }

    private async Task<Result<IReadOnlyList<RepositoryCommentDto>>> CommentsAsync(Guid owner, string repo, int number, string kind, CancellationToken ct)
    {
        var read = await gitea.GetListAsync(owner, RepositoryService.Route(repo) + "/issues/" + number + "/comments", ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        var link = gitea.Link(repo) + "/" + kind + "/" + number;
        return Result<IReadOnlyList<RepositoryCommentDto>>.Ok(json.RootElement.EnumerateArray().Take(MaxComments).Select(x => new RepositoryCommentDto(GiteaJson.Number(x, "id"),
            GiteaJson.Login(x, "user"), GiteaJson.Text(x, "body", 16000), GiteaJson.Date(x, "created_at"), link + "#issuecomment-" + GiteaJson.Number(x, "id"))).ToArray());
    }

    private static Error? Check(string repo, string state, int page) =>
        !RepositoryService.IsRepository(repo) ? RepositoriesErrors.InvalidRepository :
        !RepositoryService.IsState(state) ? RepositoriesErrors.InvalidState :
        page is < 1 or > 1000 ? RepositoriesErrors.InvalidPage : null;

    private RepositoryIssueDto Issue(string repo, JsonElement x)
    {
        var number = GiteaJson.Int(x, "number");
        return new(number, GiteaJson.Text(x, "title", 300), GiteaJson.Text(x, "state", 16), GiteaJson.Login(x, "user"), GiteaJson.Labels(x),
            GiteaJson.Items(x, "assignees").Take(10).Select(a => GiteaJson.Text(a, "login", 100)).Where(a => a.Length > 0).ToArray(),
            GiteaJson.Int(x, "comments"), GiteaJson.Date(x, "created_at"), GiteaJson.Date(x, "updated_at"), gitea.Link(repo) + "/issues/" + number);
    }

    private RepositoryPullDto Pull(string repo, JsonElement x)
    {
        var number = GiteaJson.Int(x, "number");
        var title = GiteaJson.Text(x, "title", 300);
        return new(number, title, GiteaJson.Text(x, "state", 16), GiteaJson.Flag(x, "merged"),
            GiteaJson.Flag(x, "draft") || title.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) || title.StartsWith("[WIP]", StringComparison.OrdinalIgnoreCase),
            GiteaJson.Login(x, "user"), GiteaJson.Text(GiteaJson.Child(x, "head"), "ref", 250), GiteaJson.Text(GiteaJson.Child(x, "base"), "ref", 250), GiteaJson.Labels(x),
            GiteaJson.Int(x, "comments"), GiteaJson.Date(x, "created_at"), GiteaJson.Date(x, "updated_at"), gitea.Link(repo) + "/pulls/" + number);
    }
}
