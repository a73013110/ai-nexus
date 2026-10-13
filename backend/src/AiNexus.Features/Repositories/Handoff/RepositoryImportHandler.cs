using System.Text;
using System.Text.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Handoff;

/// <summary>
/// Imports a folder snapshot file by file. Each file is re-read at the pinned commit and checked against the user's current
/// Gitea, feature and collection access; files imported before (by this job or another) are reused, so retries resume.
/// </summary>
internal sealed class RepositoryImportHandler(NexusDbContext db, RepositoryService gitea, AccessService features, ResourceAccess access,
    AttachmentService attachments, DocumentService documents) : IBackgroundJobHandler
{
    public const string JobKind = "repository-import";
    public string Kind => JobKind;

    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => await RequireAsync(job, ct) is { IsSuccess: false } denied ? denied.Error! : Result.Success;

    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var required = await RequireAsync(execution.Job, ct);
        if (!required.IsSuccess) return required.Error!;
        var row = required.Value!;
        var paths = JsonSerializer.Deserialize<string[]>(row.PathsJson) ?? [];
        int imported = 0, skipped = 0;
        for (var i = 0; i < paths.Length; i++)
        {
            await execution.CheckpointAsync($"正在匯入 {i + 1} / {paths.Length}：{Path.GetFileName(paths[i])}", i, paths.Length, ct);
            if (i > 0 && await RequireAsync(execution.Job, ct) is { IsSuccess: false } revoked) return revoked.Error!;
            var result = await ImportAsync(row, paths[i], ct);
            if (!result.IsSuccess) return result.Error!;
            if (result.Value) imported++; else skipped++;
            row.Imported = imported; row.Skipped = skipped;
        }
        await execution.CheckpointAsync(skipped > 0 ? $"已匯入 {imported} 個檔案，略過 {skipped} 個" : $"已匯入 {imported} 個檔案", paths.Length, paths.Length, ct);
        return Result.Success;
    }

    /// <summary>True when the file is in the collection (new or reused); false when it was skipped as non-text or too large.</summary>
    private async Task<Result<bool>> ImportAsync(RepositoryFolderImport row, string path, CancellationToken ct)
    {
        var reused = await db.Set<RepositoryImport>().AsNoTracking()
            .Where(x => x.OwnerId == row.OwnerId && x.CollectionId == row.CollectionId && x.BaseUrl == row.BaseUrl && x.Repository == row.Repository && x.Commit == row.Commit && x.Path == path)
            .Join(db.Set<KnowledgeDocument>().Where(x => !x.IsDeleted), x => x.DocumentId, d => d.Id, (x, d) => x.DocumentId).AnyAsync(ct);
        if (reused) return true;
        var read = await gitea.FileAsync(row.OwnerId, row.Repository, row.Commit, path, ct);
        if (read.Error == RepositoriesErrors.NotText || read.Error == RepositoriesErrors.FileLimit) return false;
        if (!read.IsSuccess) return read.Error!;
        var file = read.Value!;
        var text = $"Gitea: {row.Repository}\nPath: {file.Path}\nCommit: {file.Commit}\nSource: {file.Url}\n\n{file.Text}";
        var bytes = Encoding.UTF8.GetBytes(text);
        using var stream = new MemoryStream(bytes);
        var markdown = Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown";
        var name = string.Concat((row.Repository.Split('/')[1] + "_" + path.Replace('/', '_')).Take(170)) + (markdown ? ".md" : ".txt");
        var upload = new FormFile(stream, 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = markdown ? "text/markdown" : "text/plain" };
        var attachment = await attachments.UploadAsync(row.OwnerId, upload, ct);
        // A full quota stops the import; a file the extractor rejects (too long, no text) is skipped like a binary one.
        if (!attachment.IsSuccess)
        {
            if (attachment.Error!.Code == "attachment_quota") return attachment.Error;
            return false;
        }
        var added = await documents.AddAsync(row.OwnerId, row.CollectionId, attachment.Value!.Id, ct);
        if (!added.IsSuccess) return added.Error!;
        db.Add(new RepositoryImport { OwnerId = row.OwnerId, CollectionId = row.CollectionId, DocumentId = added.Value!.Id, Repository = row.Repository,
            Commit = row.Commit, Path = path, BaseUrl = row.BaseUrl });
        // Saved with the document, so a retry after a crash reuses it instead of adding a duplicate.
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Result<RepositoryFolderImport>> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        features.Invalidate();
        var granted = (await features.ForUserAsync(job.OwnerId, ct)).Features;
        if (!granted.Any(x => x.Id == FeatureIds.Repositories) || !granted.Any(x => x.Id == FeatureIds.Knowledge)) return RepositoriesErrors.ImportAccessRevoked;
        var row = await db.Set<RepositoryFolderImport>().SingleOrDefaultAsync(x => x.Id == job.SubjectId && x.OwnerId == job.OwnerId, ct);
        if (row is null) return RepositoriesErrors.ImportAccessRevoked;
        if (row.BaseUrl != gitea.BaseUrl) return RepositoriesErrors.ReviewHostChanged;
        if (await access.RequireAsync(job.OwnerId, row.CollectionId, "knowledge", ct, write: true) is { IsSuccess: false } denied) return denied.Error!;
        return row;
    }
}
