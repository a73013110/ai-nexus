using System.Text.Json;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Browsing;

public sealed record RepositoryPinDto(string FullName, string Description, bool Private, DateTimeOffset PinnedAt);
public sealed record RepositoryPageDto(IReadOnlyList<RepositoryDto> Items, IReadOnlyList<RepositoryPinDto> Pinned, int Page, bool HasMore);
public sealed record RepositoryRefDto(string Name, string Commit);
public sealed record RepositoryDetailDto(RepositoryDto Repository, string? Commit, IReadOnlyList<RepositoryRefDto> Branches, IReadOnlyList<RepositoryRefDto> Tags);
public sealed record RepositoryEntryDto(string Name, string Path, string Kind, long Size);
public sealed record RepositoryTreeDto(string Commit, string Path, IReadOnlyList<RepositoryEntryDto> Entries, RepositoryFileDto? Readme);

/// <summary>以使用者自己的 token 唯讀瀏覽：程式庫清單、單一程式庫（含分支與標籤）、資料夾與檔案。</summary>
internal sealed class BrowseRepositories(NexusDbContext db, RepositoryService gitea)
{
    private const int PageSize = 20;

    public static void MapList(RouteGroupBuilder routes) => routes
        .MapGet("", (string? q, int? page, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
            handler.ListAsync(user.Id, q, page ?? 1, ct).ToHttpResultAsync())
        .WithName("ListRepositories");

    public static void Map(RouteGroupBuilder repository)
    {
        repository.MapGet("", (string owner, string name, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
                handler.ReadAsync(user.Id, owner + "/" + name, ct).ToHttpResultAsync())
            .WithName("ReadRepository");
        repository.MapGet("/tree", (string owner, string name, string commit, string? path, ICurrentUser user, BrowseRepositories handler, CancellationToken ct) =>
                handler.TreeAsync(user.Id, owner + "/" + name, commit, path ?? "", ct).ToHttpResultAsync())
            .WithName("ReadRepositoryTree");
        repository.MapGet("/file", (string owner, string name, string commit, string path, ICurrentUser user, RepositoryService gitea, CancellationToken ct) =>
                gitea.FileAsync(user.Id, owner + "/" + name, commit, path, ct).ToHttpResultAsync())
            .WithName("ReadRepositoryFile");
    }

    public async Task<Result<RepositoryPageDto>> ListAsync(Guid owner, string? query, int page, CancellationToken ct)
    {
        if (page is < 1 or > 1000) return RepositoriesErrors.InvalidPage;
        var q = (query ?? "").Trim();
        if (q.Length > 100 || q.Any(char.IsControl)) return RepositoriesErrors.InvalidRepository;
        var read = await gitea.GetAsync(owner, $"api/v1/repos/search?q={Uri.EscapeDataString(q)}&page={page}&limit={PageSize}&sort=updated&order=desc", ct);
        if (!read.IsSuccess) return read.Error!;
        using var json = read.Value!;
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new ExternalServiceException(RepositoriesErrors.ResponseInvalid);
        var rows = GiteaJson.Items(json.RootElement, "data").Take(PageSize).ToArray();
        var pins = await db.Set<RepositoryPin>().AsNoTracking().Where(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl)
            .OrderByDescending(x => x.PinnedAt).Take(RepositoryPin.Limit).ToListAsync(ct);
        var pinned = pins.Select(x => x.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = rows.Select(x => Describe(x, pinned.Contains)).Where(x => RepositoryService.IsRepository(x.FullName)).ToArray();
        return new RepositoryPageDto(items, pins.Select(x => new RepositoryPinDto(x.Repository, x.Description, x.Private, x.PinnedAt)).ToArray(), page, rows.Length == PageSize);
    }

    public async Task<Result<RepositoryDetailDto>> ReadAsync(Guid owner, string repo, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        var route = RepositoryService.Route(repo);
        var read = await gitea.GetAsync(owner, route, ct);
        if (!read.IsSuccess) return read.Error!;
        using var metadata = read.Value!;
        var pinned = await db.Set<RepositoryPin>().AsNoTracking().AnyAsync(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl && x.Repository == repo, ct);
        var repository = Describe(metadata.RootElement, _ => pinned);
        if (repository.Empty) return new RepositoryDetailDto(repository, null, [], []);
        // token 此時已快取，兩份參照清單可以並行讀取。
        var branchesRead = gitea.GetListAsync(owner, route + "/branches?limit=50", ct);
        var tagsRead = gitea.GetListAsync(owner, route + "/tags?limit=50", ct);
        await Task.WhenAll(branchesRead, tagsRead);
        using var branchList = (await branchesRead).Value;
        using var tagList = (await tagsRead).Value;
        var branches = Refs(branchList, x => GiteaJson.Text(GiteaJson.Child(x, "commit"), "id", 64));
        var tags = Refs(tagList, x => GiteaJson.Text(GiteaJson.Child(x, "commit"), "sha", 64));
        var commit = branches.FirstOrDefault(x => x.Name == repository.DefaultBranch)?.Commit;
        if (commit is null && repository.DefaultBranch.Length > 0)
        {
            var head = await gitea.GetAsync(owner, route + "/branches/" + Uri.EscapeDataString(repository.DefaultBranch), ct);
            if (!head.IsSuccess) return head.Error!;
            using var branch = head.Value!;
            commit = GiteaJson.Text(GiteaJson.Child(branch.RootElement, "commit"), "id", 64);
        }
        return new RepositoryDetailDto(repository, RepositoryService.IsCommit(commit) ? commit.ToLowerInvariant() : null, branches, tags);
    }

    public async Task<Result<RepositoryTreeDto>> TreeAsync(Guid owner, string repo, string commit, string path, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!RepositoryService.IsCommit(commit)) return RepositoriesErrors.InvalidCommit;
        if (!RepositoryService.IsFilePath(path, true)) return RepositoriesErrors.InvalidPath;
        var read = await gitea.GetAsync(owner, RepositoryService.Route(repo) + "/contents" + (path.Length == 0 ? "" : "/" + RepositoryService.EncodePath(path)) + "?ref=" + commit, ct);
        if (!read.IsSuccess) return read.Error!;
        RepositoryEntryDto[] entries;
        using (var json = read.Value!)
        {
            if (json.RootElement.ValueKind != JsonValueKind.Array) return RepositoriesErrors.NotDirectory;
            entries = json.RootElement.EnumerateArray().Take(500).Select(x => new RepositoryEntryDto(GiteaJson.Text(x, "name", 180), GiteaJson.Text(x, "path", 500),
                GiteaJson.Text(x, "type", 16), GiteaJson.Number(x, "size"))).Where(x => x.Name.Length > 0)
                .OrderBy(x => x.Kind == "dir" ? 0 : 1).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        var readme = entries.Where(x => x.Kind == "file" && x.Size <= gitea.Options.MaxFileBytes && IsReadme(x.Name))
            .OrderBy(x => x.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
        var text = readme is null ? default(Result<RepositoryFileDto>?) : await gitea.FileAsync(owner, repo, commit, readme.Path, ct);
        return new RepositoryTreeDto(commit, path, entries, text is { IsSuccess: true } file ? file.Value : null);
    }

    private static bool IsReadme(string name) => Path.GetFileNameWithoutExtension(name).Equals("README", StringComparison.OrdinalIgnoreCase) &&
        Path.GetExtension(name).ToLowerInvariant() is "" or ".md" or ".markdown" or ".txt";

    private static RepositoryRefDto[] Refs(JsonDocument? list, Func<JsonElement, string> commit) => list is null ? [] : list.RootElement.EnumerateArray().Take(50)
        .Select(x => new RepositoryRefDto(GiteaJson.Text(x, "name", 250), commit(x).ToLowerInvariant()))
        .Where(x => RepositoryService.IsRef(x.Name) && RepositoryService.IsCommit(x.Commit)).ToArray();

    internal RepositoryDto Describe(JsonElement x, Func<string, bool> pinned)
    {
        var name = GiteaJson.Text(x, "full_name", 201);
        return new(name, GiteaJson.Text(x, "description", 500), GiteaJson.Flag(x, "private"), GiteaJson.Flag(x, "archived"), GiteaJson.Flag(x, "empty"),
            GiteaJson.Text(x, "default_branch", 250), GiteaJson.Text(x, "language", 60), GiteaJson.Int(x, "stars_count"), GiteaJson.Int(x, "forks_count"),
            GiteaJson.Int(x, "open_issues_count"), GiteaJson.Int(x, "open_pr_counter"), GiteaJson.Date(x, "updated_at"),
            RepositoryService.IsRepository(name) ? gitea.Link(name) : gitea.BaseUrl, pinned(name));
    }
}
