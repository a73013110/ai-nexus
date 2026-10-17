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
/// 逐檔匯入資料夾快照。每個檔案都在固定 commit 上重新讀取，並檢查使用者目前的 Gitea、功能與知識庫權限；
/// 先前已匯入的檔案（不論是否為本工作匯入）直接沿用，因此重試可以接續進行。
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
            // 從第二個檔案開始每次都重新檢查權限：匯入可能跑好幾分鐘，中途撤權或關閉功能就要停止。
            if (i > 0 && await RequireAsync(execution.Job, ct) is { IsSuccess: false } revoked) return revoked.Error!;
            var result = await ImportAsync(row, paths[i], ct);
            if (!result.IsSuccess) return result.Error!;
            if (result.Value) imported++; else skipped++;
            row.Imported = imported; row.Skipped = skipped;
        }
        await execution.CheckpointAsync(skipped > 0 ? $"已匯入 {imported} 個檔案，略過 {skipped} 個" : $"已匯入 {imported} 個檔案", paths.Length, paths.Length, ct);
        return Result.Success;
    }

    /// <summary>檔案已在知識庫中（新匯入或沿用既有文件）回傳 true；因非文字或過大而略過回傳 false。</summary>
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
        var stem = row.Repository.Split('/')[1] + "_" + path.Replace('/', '_');
        var name = string.Concat(stem.Take(170)) + (markdown ? ".md" : ".txt");
        var upload = new FormFile(stream, 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = markdown ? "text/markdown" : "text/plain" };
        var attachment = await attachments.UploadAsync(row.OwnerId, upload, ct);
        // 配額用完會中止整個匯入；抽取器拒絕的檔案（太長、沒有文字）與二進位檔一樣略過。
        if (!attachment.IsSuccess)
        {
            if (attachment.Error!.Code == "attachment_quota") return attachment.Error;
            return false;
        }
        var added = await documents.AddAsync(row.OwnerId, row.CollectionId, attachment.Value!.Id, ct);
        if (!added.IsSuccess) return added.Error!;
        db.Add(new RepositoryImport { OwnerId = row.OwnerId, CollectionId = row.CollectionId, DocumentId = added.Value!.Id, Repository = row.Repository,
            Commit = row.Commit, Path = path, BaseUrl = row.BaseUrl });
        // 與文件同一次儲存；若當機後重試，就會沿用既有紀錄，而不是再新增一份重複的文件。
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Result<RepositoryFolderImport>> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        features.Invalidate();
        var granted = (await features.ForUserAsync(job.OwnerId, ct)).Features;
        if (!granted.Any(x => x.Id == FeatureIds.Repositories) || !granted.Any(x => x.Id == FeatureIds.Knowledge)) return AccessControlErrors.FeatureForbidden;
        var row = await db.Set<RepositoryFolderImport>().SingleOrDefaultAsync(x => x.Id == job.SubjectId && x.OwnerId == job.OwnerId, ct);
        if (row is null) return CollaborationErrors.ResourceNotFound;
        if (row.BaseUrl != gitea.BaseUrl) return RepositoriesErrors.ReviewHostChanged;
        if (await access.RequireAsync(job.OwnerId, row.CollectionId, "knowledge", ct, write: true) is { IsSuccess: false } denied) return denied.Error!;
        return row;
    }
}
