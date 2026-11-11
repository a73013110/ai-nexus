using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Platform.Errors;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Knowledge.Indexing;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed class EmbeddingReindexHandler(AppDbContext db, EmbeddingLifecycle lifecycle, EmbeddingVectorStore vectors, DocumentIndexer indexer,
    AccessService access, IOptions<KnowledgeOptions> options, AdministrativeWriteLock writes, AuditTrail audit) : IBackgroundJobHandler
{
    public const string JobKind = "embedding-reindex";
    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => (await ProfileAsync(job, ct)) is { IsSuccess: false } invalid ? invalid.Error : Result.Success;

    /// <summary>取得要重建的向量模型設定；擁有者仍需具備檢索管理頁的授權，且模型未退役。</summary>
    private async Task<Result<EmbeddingProfile>> ProfileAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!await access.HoldsAsync(job.OwnerId, FeatureCatalog.KnowledgeRetrieval, ct)) return AccessControlErrors.FeatureForbidden;
        var profile = await lifecycle.RequireAsync(EmbeddingJobs.Profile(job.SubjectId), ct);
        if (profile.IsSuccess && profile.Value.Status == "retired") return KnowledgeErrors.ProfileRetired;
        return profile;
    }

    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var found = await ProfileAsync(execution.Job, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        // 已經完成的文件一句查詢濾掉，不逐份檢查：重建被中斷後接續、或換模型時多數文件的向量都還在，逐份要各打數次查詢再寫一次進度。
        // 有工作正在改寫、或缺頁的文件不算在內，留給下面的迴圈照原規則處理（略過或失敗）。
        var documents = db.Documents.Where(x => x.CollectionId != null && !x.IsDeleted);
        var settled = Indexed(profile).Where(d => db.DocumentPages.Any(p => p.DocumentId == d.Id)
            && !db.BackgroundJobs.Any(j => j.SubjectId == d.Id && j.ActiveKey != null));
        var total = await documents.CountAsync(ct);
        var ids = await documents.Where(d => !settled.Any(x => x.Id == d.Id)).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var done = total - ids.Count;
        if (done > 0 && ids.Count > 0) await execution.CheckpointAsync("逐文件重建索引", done, total, ct);
        foreach (var id in ids)
        {
            if (await ValidateRetryAsync(execution.Job, ct) is { IsSuccess: false } revoked) return revoked.Error;
            // 匯入或嵌入工作正在改寫這份文件，由它們自己的檢查點填入此向量模型，這裡直接略過。
            if (await db.BackgroundJobs.AnyAsync(x => x.SubjectId == id && x.ActiveKey != null, ct)) continue;
            var doc = await db.Documents.SingleAsync(x => x.Id == id, ct);
            if (!await db.DocumentPages.AnyAsync(x => x.DocumentId == id, ct)) return KnowledgeErrors.DocumentPagesMissing;
            // 查詢之後才由別的工作補完的文件不必再索引一次。
            var indexed = await Indexed(profile).AnyAsync(x => x.Id == id, ct);
            if (!indexed && await indexer.IndexAsync(execution, doc, profile, ct) is { IsSuccess: false } failed) return failed;
            await execution.CheckpointAsync("逐文件重建索引", ++done, total, ct);
            db.ChangeTracker.Clear();
        }
        var coverage = await lifecycle.CoverageAsync(profile, ct);
        await execution.CheckpointAsync("索引片段覆蓋率", coverage.CompletedChunks, coverage.TotalChunks, ct);
        if (!coverage.Complete) return KnowledgeErrors.ProfileIncomplete;
        if (options.Value.Embedding.AutoActivate && profile.Status == "building")
        {
            Error? failure = null;
            await execution.CheckpointAsync("啟用完成的索引", coverage.CompletedChunks, coverage.TotalChunks, ct, async () => {
                if (await lifecycle.ActivateCoreAsync(profile.Id, ct) is { IsSuccess: false } refused) { failure = refused.Error; return; }
                audit.Record(KnowledgeAudit.ProfileActivated, execution.Job.OwnerId, details: new { profileId = profile.Id, automatic = true }); await db.SaveChangesAsync(ct);
            }, first: () => writes.AcquireAsync(KnowledgeLocks.EmbeddingProfile, ct));
            if (failure is not null) return failure;
        }
        return Result.Success;
    }
    /// <summary>
    /// 這個 profile 的向量已完整的文件：狀態 ready、有片段，且每個片段都有向量（不需要向量的 provider 只看前兩項）。
    /// 以存在性判斷而不比筆數，一句查詢就能套在整批文件上。
    /// </summary>
    private IQueryable<Document> Indexed(EmbeddingProfile profile)
    {
        var ready = db.Documents.Where(d => d.Status == "ready" && db.Chunks.Any(c => c.DocumentId == d.Id));
        if (profile.Provider == "none") return ready;
        var rows = vectors.Rows(profile);
        return ready.Where(d => !db.Chunks.Any(c => c.DocumentId == d.Id && !rows.Any(e => e.ChunkId == c.Id)));
    }
}
