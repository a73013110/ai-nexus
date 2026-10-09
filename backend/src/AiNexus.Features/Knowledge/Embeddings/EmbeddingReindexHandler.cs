using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class EmbeddingReindexHandler(NexusDbContext db, EmbeddingLifecycle lifecycle, DocumentIndexer indexer,
    AccessService access, IOptions<KnowledgeOptions> options, KnowledgeWriteLock writes) : IBackgroundJobHandler
{
    public string Kind => "embedding-reindex";
    public async Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!(await access.ForUserAsync(job.OwnerId, ct)).Features.Any(x => x.Id == FeatureIds.Admin)) throw new ApiException(403, "admin_required", "重建全量索引需要管理權限。");
        if ((await lifecycle.RequireAsync(EmbeddingJobs.Profile(job.SubjectId), ct)).Status == "retired") throw new ApiException(409, "profile_retired", "索引已退役，請建立新的重建工作。");
    }
    public async Task ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        await ValidateRetryAsync(execution.Job, ct);
        var profile = await lifecycle.RequireAsync(EmbeddingJobs.Profile(execution.Job.SubjectId), ct);
        var ids = await db.Set<KnowledgeDocument>().AsNoTracking().Where(x => x.CollectionId != null && !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var done = 0;
        foreach (var id in ids)
        {
            await ValidateRetryAsync(execution.Job, ct);
            // An ingest/embedding job owns a changing document; its own checkpoints will fill this profile.
            if (await db.Set<BackgroundJob>().AnyAsync(x => x.SubjectId == id && x.ActiveKey != null, ct)) continue;
            var doc = await db.Set<KnowledgeDocument>().SingleAsync(x => x.Id == id, ct);
            if (!await db.Set<DocumentPage>().AnyAsync(x => x.DocumentId == id, ct)) throw new ApiException(409, "document_pages_missing", "文件尚無完整逐頁文字，請重新上傳資料後再重建。");
            var indexed = await VectorsCompleteAsync(doc, profile, ct);
            if (!indexed) await indexer.IndexAsync(execution, doc, profile, ct);
            await execution.CheckpointAsync("逐文件重建索引", ++done, ids.Count, ct);
            db.ChangeTracker.Clear();
        }
        var coverage = await lifecycle.CoverageAsync(profile, ct);
        await execution.CheckpointAsync("索引片段覆蓋率", coverage.CompletedChunks, coverage.TotalChunks, ct);
        if (!coverage.Complete) throw new ApiException(409, "profile_incomplete", "仍有文件正在處理或缺少向量，完成後可重試此重建工作。");
        if (options.Value.AutoActivate && profile.Status == "building")
        {
            await writes.Gate.WaitAsync(ct);
            try {
                await execution.CheckpointAsync("啟用完成的索引", coverage.CompletedChunks, coverage.TotalChunks, ct, async () => {
                    await lifecycle.ActivateCoreAsync(profile.Id, ct); db.AuditEvents.Add(new() { OwnerId = execution.Job.OwnerId, Action = "system.embedding_activate", Result = "activated", DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { profile.Id }) }); await db.SaveChangesAsync(ct);
                }, isolation: System.Data.IsolationLevel.Serializable);
            } finally { writes.Gate.Release(); }
        }
    }
    private async Task<bool> VectorsCompleteAsync(KnowledgeDocument doc, EmbeddingProfile profile, CancellationToken ct)
    {
        if (doc.Status != "ready") return false;
        var chunks = await db.Set<KnowledgeChunk>().CountAsync(x => x.DocumentId == doc.Id, ct);
        if (chunks == 0) return false;
        if (profile.Provider == "none") return true;
        var embeddings = profile.Dimensions == 768
            ? await (from e in db.Set<ChunkEmbedding768>() join c in db.Set<KnowledgeChunk>() on e.ChunkId equals c.Id where e.ProfileId == profile.Id && c.DocumentId == doc.Id select e.Id).CountAsync(ct)
            : await (from e in db.Set<ChunkEmbedding1024>() join c in db.Set<KnowledgeChunk>() on e.ChunkId equals c.Id where e.ProfileId == profile.Id && c.DocumentId == doc.Id select e.Id).CountAsync(ct);
        return chunks == embeddings;
    }
}
