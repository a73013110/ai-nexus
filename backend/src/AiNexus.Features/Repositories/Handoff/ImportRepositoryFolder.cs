using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Jobs;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;

namespace AiNexus.Features.Repositories.Handoff;

[ValidatedInHandler("Repository, commit and path share the public codes of the browsing endpoints; collection access is checked before them.")]
public sealed record ImportRepositoryFolderRequest(string Commit, string Path, Guid CollectionId);
public sealed record RepositoryFolderImportDto(Guid Id, int Files, JobDto Job);

/// <summary>
/// 列出固定 commit 上某資料夾底下的文字檔，並把匯入排入知識庫的佇列。
/// 檔案清單在此時固定，因此工作重試時匯入的仍是同一份快照。
/// </summary>
internal sealed class ImportRepositoryFolder(NexusDbContext db, RepositoryService gitea, ResourceAccess access, JobService jobs, RepositoryWriteLock writes)
{
    // 值得當成文字建立索引的副檔名；其他多半是二進位或產生的檔案，直接略過。
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".rst", ".adoc", ".cs", ".csproj", ".props", ".targets", ".slnx", ".config", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs",
        ".json", ".yaml", ".yml", ".toml", ".ini", ".xml", ".html", ".htm", ".css", ".scss", ".less", ".vue", ".py", ".java", ".kt", ".go", ".rs", ".rb", ".php",
        ".sql", ".sh", ".ps1", ".psm1", ".bat", ".cmd", ".c", ".h", ".cpp", ".hpp", ".swift", ".dart", ".proto", ".graphql", ".gradle", ".csv", ".log",
    };
    private static readonly HashSet<string> TextNames = new(StringComparer.OrdinalIgnoreCase) { "README", "LICENSE", "CHANGELOG", "Dockerfile", "Makefile" };

    public static RouteHandlerBuilder Map(RouteGroupBuilder repository) => repository
        .MapPost("/imports", (string owner, string name, ImportRepositoryFolderRequest body, ICurrentUser user, ImportRepositoryFolder handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, owner + "/" + name, body, ct).ToHttpResultAsync())
        .RequireAuthorization(Policies.Knowledge).RequireRateLimiting(RepositoriesModule.WriteRateLimit).WithName("ImportRepositoryFolder");

    public async Task<Result<RepositoryFolderImportDto>> HandleAsync(Guid owner, string repo, ImportRepositoryFolderRequest request, CancellationToken ct)
    {
        if (await access.RequireAsync(owner, request.CollectionId, "knowledge", ct, write: true) is { IsSuccess: false } denied) return denied.Error!;
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        if (!RepositoryService.IsCommit(request.Commit)) return RepositoriesErrors.InvalidCommit;
        var folder = request.Path.Trim('/');
        if (!RepositoryService.IsFilePath(folder, true)) return RepositoriesErrors.InvalidPath;
        var commit = request.Commit.ToLowerInvariant();
        var listed = await ListAsync(owner, repo, commit, folder, ct);
        if (!listed.IsSuccess) return listed.Error!;
        var paths = listed.Value!;
        if (paths.Count == 0) return RepositoriesErrors.ImportEmpty;
        if (paths.Count > gitea.Options.MaxImportFiles) return RepositoriesErrors.ImportLimit;
        await writes.Gate.WaitAsync(ct);
        try
        {
            var row = new RepositoryFolderImport { OwnerId = owner, BaseUrl = gitea.BaseUrl, Repository = repo, Commit = commit, Path = folder,
                CollectionId = request.CollectionId, PathsJson = JsonSerializer.Serialize(paths) };
            var label = repo.Split('/')[1] + (folder.Length > 0 ? "/" + folder : "") + " · " + commit[..7];
            var job = jobs.Enqueue(owner, request.CollectionId, row.Id, RepositoryImportHandler.JobKind, string.Concat(label.Take(180)));
            job.TotalUnits = paths.Count;
            row.JobId = job.Id;
            db.Add(row);
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = request.CollectionId, Action = "repository.imported", Result = "queued" });
            await db.SaveChangesAsync(ct);
            return new RepositoryFolderImportDto(row.Id, paths.Count, JobService.Describe(job));
        }
        finally { writes.Gate.Release(); }
    }

    /// <summary>從 Gitea 的遞迴樹列出資料夾下的文字檔；超過上限就提早停止。</summary>
    private async Task<Result<IReadOnlyList<string>>> ListAsync(Guid owner, string repo, string commit, string folder, CancellationToken ct)
    {
        var route = RepositoryService.Route(repo);
        var tree = commit;
        if (folder.Length > 0)
        {
            var parent = folder.Contains('/') ? folder[..folder.LastIndexOf('/')] : "";
            var read = await gitea.GetAsync(owner, route + "/contents" + (parent.Length == 0 ? "" : "/" + RepositoryService.EncodePath(parent)) + "?ref=" + commit, ct);
            if (!read.IsSuccess) return read.Error!;
            using var listing = read.Value!;
            var name = folder[(folder.LastIndexOf('/') + 1)..];
            tree = GiteaJson.Items(listing.RootElement).Where(x => GiteaJson.Text(x, "name", 180) == name && GiteaJson.Text(x, "type", 16) == "dir")
                .Select(x => GiteaJson.Text(x, "sha", 64)).FirstOrDefault() ?? "";
            if (!RepositoryService.IsCommit(tree)) return RepositoriesErrors.NotDirectory;
        }
        var paths = new List<string>();
        // 最多讀 20 頁（每頁 1000 筆），避免超大的程式庫無止境地列舉。
        for (var page = 1; page <= 20; page++)
        {
            var read = await gitea.GetAsync(owner, route + "/git/trees/" + tree + $"?recursive=true&page={page}&per_page=1000", ct);
            if (!read.IsSuccess) return read.Error!;
            using var json = read.Value!;
            foreach (var entry in GiteaJson.Items(json.RootElement, "tree"))
            {
                var path = GiteaJson.Text(entry, "path", 500);
                if (GiteaJson.Text(entry, "type", 16) != "blob" || GiteaJson.Number(entry, "size") is < 1 || GiteaJson.Number(entry, "size") > gitea.Options.MaxFileBytes) continue;
                var full = folder.Length == 0 ? path : folder + "/" + path;
                var fileName = Path.GetFileName(path);
                if (!RepositoryService.IsFilePath(full, false) || !(TextExtensions.Contains(Path.GetExtension(fileName)) || TextNames.Contains(Path.GetFileNameWithoutExtension(fileName)))) continue;
                paths.Add(full);
                if (paths.Count > gitea.Options.MaxImportFiles) return Result<IReadOnlyList<string>>.Ok(paths);
            }
            if (!GiteaJson.Flag(json.RootElement, "truncated")) break;
        }
        return Result<IReadOnlyList<string>>.Ok(paths);
    }
}
