using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Data.App.Repositories;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Files;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Documents;
using AiNexus.Platform.Errors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Strings;

namespace AiNexus.Features.Work.Repositories.Handoff;

/// <summary>
/// 逐檔匯入資料夾快照。每個檔案都在固定 commit 上重新讀取，並檢查使用者目前的 Gitea、功能與知識庫權限；
/// 先前已匯入的檔案（不論是否為本工作匯入）直接沿用，因此重試可以接續進行。
/// 來源紀錄的唯一索引保證同一路徑只有一份文件：工作重疊時後到的回復自己的文件，改為沿用。
/// </summary>
internal sealed class RepositoryImportHandler(AppDbContext db, RepositoryService gitea, AccessService features, ResourceAccess access,
    AttachmentService attachments, DocumentService documents) : IBackgroundJobHandler
{
    public const string JobKind = "repository-import";
    private const string UniqueIndex = "ux_RepositoryImports";

    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => await RequireAsync(job, ct) is { IsSuccess: false } denied ? denied.Error : Result.Success;

    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var required = await RequireAsync(execution.Job, ct);
        if (!required.IsSuccess) return required.Error;
        var row = required.Value;
        var paths = JsonSerializer.Deserialize<string[]>(row.PathsJson) ?? [];
        int imported = 0, skipped = 0;
        for (var i = 0; i < paths.Length; i++)
        {
            await execution.CheckpointAsync($"正在匯入 {i + 1} / {paths.Length}：{Path.GetFileName(paths[i])}", i, paths.Length, ct);
            // 從第二個檔案開始每次都重新檢查權限：匯入可能跑好幾分鐘，中途撤權或關閉功能就要停止。
            if (i > 0 && await RequireAsync(execution.Job, ct) is { IsSuccess: false } revoked) return revoked.Error;
            var result = await ImportAsync(row, paths[i], ct);
            if (!result.IsSuccess) return result.Error;
            if (result.Value) imported++; else skipped++;
            row.Imported = imported; row.Skipped = skipped;
        }
        await execution.CheckpointAsync(skipped > 0 ? $"已匯入 {imported} 個檔案，略過 {skipped} 個" : $"已匯入 {imported} 個檔案", paths.Length, paths.Length, ct);
        return Result.Success;
    }

    /// <summary>檔案已在知識庫中（新匯入或沿用既有文件）回傳 true；因非文字或過大而略過回傳 false。</summary>
    private async Task<Result<bool>> ImportAsync(RepositoryFolderImport row, string path, CancellationToken ct)
    {
        var reused = await Source(row, path).AsNoTracking()
            .Join(db.Documents.Where(x => !x.IsDeleted), x => x.DocumentId, d => d.Id, (x, d) => x.DocumentId).AnyAsync(ct);
        if (reused) return true;
        var read = await gitea.FileAsync(row.OwnerId, row.Repository, row.Commit, path, ct);
        if (read.Error == RepositoriesErrors.NotText || read.Error == RepositoriesErrors.FileLimit) return false;
        if (!read.IsSuccess) return read.Error;
        var file = read.Value;
        var text = $"Gitea: {row.Repository}\nPath: {file.Path}\nCommit: {file.Commit}\nSource: {file.Url}\n\n{file.Text}";
        var bytes = Encoding.UTF8.GetBytes(text);
        using var stream = new MemoryStream(bytes);
        var markdown = Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown";
        var stem = row.Repository.Split('/')[1] + "_" + path.Replace('/', '_');
        var name = stem.Clip(170) + (markdown ? ".md" : ".txt");
        var upload = new FormFile(stream, 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = markdown ? "text/markdown" : "text/plain" };
        var attachment = await attachments.UploadAsync(row.OwnerId, upload, ct);
        // 配額用完會中止整個匯入；抽取器拒絕的檔案（太長、沒有文字）與二進位檔一樣略過。
        if (!attachment.IsSuccess)
        {
            if (attachment.Error.Code == "attachment_quota") return attachment.Error;
            return false;
        }
        // 文件被刪除後紀錄還在：程式不沿用它，但唯一索引會擋住重新匯入。
        await Source(row, path).Where(x => db.Documents.Any(d => d.Id == x.DocumentId && d.IsDeleted)).ExecuteDeleteAsync(ct);
        Result<DocumentDto> added;
        try
        {
            // 紀錄與文件同一個交易：當機後重試不會多一份文件，撞上唯一索引時文件也一起回復。
            added = await documents.AddAsync(row.OwnerId, row.CollectionId, attachment.Value.Id, ct, alongside: document => db.Add(new RepositoryImport { OwnerId = row.OwnerId, CollectionId = row.CollectionId,
                DocumentId = document.Id, Repository = row.Repository, Commit = row.Commit, Path = path, BaseUrl = row.BaseUrl }));
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains(UniqueIndex, StringComparison.OrdinalIgnoreCase))
        {
            // 另一個工作先匯入了同一路徑，等於沿用。剛上傳的檔案沒有引用，由保留期清理。
            // 失敗的新增還留在追蹤器，不移除的話下一次儲存會再送一次。
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToList()) entry.State = EntityState.Detached;
            return true;
        }
        if (!added.IsSuccess) return added.Error;
        return true;
    }

    // 不比對 BaseUrl：同一個 repository、commit 與路徑的內容相同，換主機也應該沿用；放進索引也會超過鍵長上限。
    private IQueryable<RepositoryImport> Source(RepositoryFolderImport row, string path) => db.RepositoryImports
        .Where(x => x.OwnerId == row.OwnerId && x.CollectionId == row.CollectionId && x.Repository == row.Repository && x.Commit == row.Commit && x.Path == path);

    private async Task<Result<RepositoryFolderImport>> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        features.Invalidate();
        var granted = (await features.ForUserAsync(job.OwnerId, ct)).Features;
        if (!granted.Any(x => x.Id == FeatureCatalog.Repositories) || !granted.Any(x => x.Id == FeatureCatalog.Knowledge)) return AccessControlErrors.FeatureForbidden;
        var row = await db.RepositoryFolderImports.SingleOrDefaultAsync(x => x.Id == job.SubjectId && x.OwnerId == job.OwnerId, ct);
        if (row is null) return CollaborationErrors.ResourceNotFound;
        if (row.BaseUrl != gitea.BaseUrl) return RepositoriesErrors.ReviewHostChanged;
        if (await access.RequireAsync(job.OwnerId, row.CollectionId, Collection.Kind, ct, write: true) is { IsSuccess: false } denied) return denied.Error;
        return row;
    }
}
