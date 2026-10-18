using System.Text.Json;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Repositories.Browsing;

public sealed record RepositoryCommitDto(string Sha, string Title, string Message, string Author, DateTimeOffset? At, IReadOnlyList<string> Parents);
public sealed record RepositoryCommitPageDto(IReadOnlyList<RepositoryCommitDto> Items, int Page, bool HasMore);
/// <summary>同一 commit 相對於父 commit 的差異，或相對於 <see cref="Base"/> 的淨變更；<see cref="TooLarge"/> 為 true 時 <see cref="Diff"/> 為空。</summary>
public sealed record RepositoryDiffDto(RepositoryCommitDto Head, RepositoryCommitDto? Base, string Diff, bool TooLarge);

/// <summary>變更歷史：某個 ref 的 commit 清單，以及單一 commit 或兩個 commit 之間的 diff。</summary>
internal sealed class BrowseRepositoryHistory(RepositoryService gitea)
{
    private const int PageSize = 30;

    public static void Map(RouteGroupBuilder repository)
    {
        repository.MapGet("/commits", (string owner, string name, string? @ref, int? page, ICurrentUser user, BrowseRepositoryHistory handler, CancellationToken ct) =>
                handler.CommitsAsync(user.Id, owner + "/" + name, @ref, page ?? 1, ct).ToHttpResultAsync())
            .WithName("ListRepositoryCommits");
        repository.MapGet("/diff", (string owner, string name, string head, string? @base, ICurrentUser user, BrowseRepositoryHistory handler, CancellationToken ct) =>
                handler.DiffAsync(user.Id, owner + "/" + name, head, @base, ct).ToHttpResultAsync())
            .WithName("ReadRepositoryDiff");
    }

    public async Task<Result<RepositoryCommitPageDto>> CommitsAsync(Guid owner, string repo, string? reference, int page, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (reference is not null && !RepositoryService.IsRef(reference)) return RepositoriesErrors.InvalidRef;
        if (page is < 1 or > 1000) return RepositoriesErrors.InvalidPage;
        var query = (reference is null ? "" : "sha=" + Uri.EscapeDataString(reference) + "&") + $"page={page}&limit={PageSize}&stat=false&verification=false&files=false";
        var read = await gitea.GetListAsync(owner, RepositoryService.Route(repo) + "/commits?" + query, ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        var rows = json.RootElement.EnumerateArray().Take(PageSize).ToArray();
        return new RepositoryCommitPageDto(rows.Select(Commit).Where(x => RepositoryService.IsCommit(x.Sha)).ToArray(), page, rows.Length == PageSize);
    }

    public async Task<Result<RepositoryDiffDto>> DiffAsync(Guid owner, string repo, string head, string? basis, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!RepositoryService.IsCommit(head) || (basis is not null && !RepositoryService.IsCommit(basis))) return RepositoriesErrors.InvalidCommit;
        if (string.Equals(head, basis, StringComparison.OrdinalIgnoreCase)) return RepositoriesErrors.ReviewEmptyRange;
        var headCommit = await ReadCommitAsync(owner, repo, head, ct);
        if (!headCommit.IsSuccess) return headCommit.Error!;
        RepositoryCommitDto? baseCommit = null;
        if (basis is not null)
        {
            var read = await ReadCommitAsync(owner, repo, basis, ct);
            if (!read.IsSuccess) return read.Error!;
            baseCommit = read.Value;
        }
        var diff = await gitea.DiffAsync(owner, repo, head.ToLowerInvariant(), basis?.ToLowerInvariant(), ct);
        if (diff.Error == RepositoriesErrors.DiffLimit) return new RepositoryDiffDto(headCommit.Value!, baseCommit, "", true);
        if (!diff.IsSuccess) return diff.Error!;
        return new RepositoryDiffDto(headCommit.Value!, baseCommit, diff.Value!, false);
    }

    private async Task<Result<RepositoryCommitDto>> ReadCommitAsync(Guid owner, string repo, string sha, CancellationToken ct)
    {
        var read = await gitea.GetAsync(owner, RepositoryService.Route(repo) + "/git/commits/" + sha + "?stat=false&files=false&verification=false", ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        var commit = Commit(json.RootElement);
        return RepositoryService.IsCommit(commit.Sha) ? commit : throw new ExternalServiceException(RepositoriesErrors.ResponseInvalid);
    }

    internal static RepositoryCommitDto Commit(JsonElement x)
    {
        var detail = GiteaJson.Child(x, "commit");
        var message = GiteaJson.Text(detail, "message", 4000).Replace("\r\n", "\n").TrimEnd();
        var author = GiteaJson.Child(detail, "author");
        var login = GiteaJson.Login(x, "author");
        return new(GiteaJson.Text(x, "sha", 64).ToLowerInvariant(), message.Split('\n')[0], message, login.Length > 0 ? login : GiteaJson.Text(author, "name", 100),
            GiteaJson.Date(author, "date"), GiteaJson.Items(x, "parents").Select(p => GiteaJson.Text(p, "sha", 64).ToLowerInvariant()).Where(RepositoryService.IsCommit).Take(8).ToArray());
    }
}
