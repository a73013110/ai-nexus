using System.Globalization;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Administration;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Knowledge;

public sealed record EmbeddingCoverageDto(int CompletedChunks, int TotalChunks, int PendingDocuments)
{
    public bool Complete => CompletedChunks == TotalChunks && PendingDocuments == 0;
    public double Ratio => TotalChunks == 0 ? (PendingDocuments == 0 ? 1 : 0) : (double)CompletedChunks / TotalChunks;
}
public sealed record EmbeddingProfileDto(int Id, string Key, string Provider, string Model, int Dimensions, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? RetiredAt, EmbeddingCoverageDto Coverage, JobDto? Job);
public static class EmbeddingJobs
{
    public static Guid Subject(int profile) => new($"{profile:x8}-bfac-4dc0-a11a-000000000000");
    public static int Profile(Guid subject) => int.Parse(subject.ToString("N")[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
public sealed class EmbeddingLifecycle(NexusDbContext db, EmbeddingProfiles profiles, EmbeddingVectorStore vectors, KnowledgeWriteLock writes,
    AdministrativeAudit audit, CurrentUser current, JobService jobs, IOptions<KnowledgeOptions> options)
{
    private IQueryable<KnowledgeDocument> Documents => db.Set<KnowledgeDocument>().Where(x => x.CollectionId != null && !x.IsDeleted);
    public async Task<EmbeddingCoverageDto> CoverageAsync(EmbeddingProfile profile, CancellationToken ct)
    {
        var chunks = from c in db.Set<KnowledgeChunk>() join d in Documents on c.DocumentId equals d.Id select c;
        var total = await chunks.CountAsync(ct);
        var completed = profile.Provider == "none" ? total : await (from e in vectors.Rows(profile) join c in chunks on e.ChunkId equals c.Id select e.ChunkId).CountAsync(ct);
        return new(completed, total, await Documents.CountAsync(x => x.Status != "ready", ct));
    }
    public async Task<IReadOnlyList<EmbeddingProfileDto>> ListAsync(CancellationToken ct)
    {
        await profiles.TargetAsync(ct);
        var all = await db.Set<EmbeddingProfile>().AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(ct);
        var tasks = await db.Set<BackgroundJob>().AsNoTracking().Where(x => x.Kind == "embedding-reindex").OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var result = new List<EmbeddingProfileDto>();
        foreach (var p in all) result.Add(new(p.Id, p.Key, p.Provider, p.Model, p.Dimensions, p.Status, p.CreatedAt, p.ActivatedAt, p.RetiredAt,
            await CoverageAsync(p, ct), tasks.FirstOrDefault(x => x.SubjectId == EmbeddingJobs.Subject(p.Id)) is { } job ? JobService.Describe(job) : null));
        return result;
    }
    public async Task<JobDto> StartAsync(int id, CancellationToken ct)
    {
        var actor = (await current.GetAsync(ct)).Id; BackgroundJob? job = null;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await audit.MutateAsync("admin.embedding_rebuild", null, id.ToString(CultureInfo.InvariantCulture), async () => {
                var profile = await RequireAsync(id, ct);
                if (profile.Status == "retired") throw new ApiException(409, "profile_retired", "已退役的索引無法重建。");
                var subject = EmbeddingJobs.Subject(id);
                job = await db.Set<BackgroundJob>().SingleOrDefaultAsync(x => x.Kind == "embedding-reindex" && x.SubjectId == subject && x.ActiveKey != null, ct)
                    ?? jobs.Enqueue(actor, null, subject, "embedding-reindex", $"重建 {profile.Model} 檢索索引");
            }, ct);
        }
        finally { writes.Gate.Release(); }
        return JobService.Describe(job!);
    }
    public async Task ActivateAsync(int id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try { await audit.MutateAsync("admin.embedding_activate", null, id.ToString(CultureInfo.InvariantCulture), () => ActivateCoreAsync(id, ct), ct); }
        finally { writes.Gate.Release(); }
    }
    internal async Task ActivateCoreAsync(int id, CancellationToken ct)
    {
        var profile = await RequireAsync(id, ct);
        if (profile.Status == "active") return;
        if (profile.Status != "building") throw new ApiException(409, "profile_not_building", "只能啟用重建中的索引。");
        if (!(await CoverageAsync(profile, ct)).Complete) throw new ApiException(409, "profile_incomplete", "索引覆蓋率未達 100%，或仍有處理中的文件，請先完成重建。");
        var now = DateTimeOffset.UtcNow;
        await db.Set<EmbeddingProfile>().Where(x => x.Status == "active").ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "retired").SetProperty(x => x.RetiredAt, now), ct);
        await db.Set<EmbeddingProfile>().Where(x => x.Id == id && x.Status == "building").ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "active").SetProperty(x => x.ActivatedAt, now).SetProperty(x => x.RetiredAt, (DateTimeOffset?)null), ct);
    }
    public async Task ClearAsync(int id, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try { await audit.MutateAsync("admin.embedding_clear", null, id.ToString(CultureInfo.InvariantCulture), () => ClearCoreAsync(id, ct), ct); }
        finally { writes.Gate.Release(); }
    }
    private async Task ClearCoreAsync(int id, CancellationToken ct)
    {
        if ((await RequireAsync(id, ct)).Status != "retired") throw new ApiException(409, "profile_not_retired", "只能清除已退役索引的向量。");
        await db.Set<ChunkEmbedding768>().Where(x => x.ProfileId == id).ExecuteDeleteAsync(ct);
        await db.Set<ChunkEmbedding1024>().Where(x => x.ProfileId == id).ExecuteDeleteAsync(ct);
    }
    internal async Task CleanupExpiredAsync(CancellationToken ct)
    {
        var expired = (await db.Set<EmbeddingProfile>().AsNoTracking().Where(x => x.Status == "retired" && x.RetiredAt != null).ToListAsync(ct))
            .Where(x => x.RetiredAt < DateTimeOffset.UtcNow.AddDays(-options.Value.RetiredRetentionDays)).ToArray();
        if (expired.Length == 0) return;
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            foreach (var profile in expired)
            {
                if (await vectors.Rows(profile).AnyAsync(ct)) {
                    await ClearCoreAsync(profile.Id, ct); db.AuditEvents.Add(new() { OwnerId = Guid.Empty, Action = "system.embedding_retention", Result = "cleared", DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { profile.Id }) });
                }
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<EmbeddingProfile> RequireAsync(int id, CancellationToken ct) => await db.Set<EmbeddingProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "profile_not_found", "找不到這個檢索索引。");
}
public sealed class EmbeddingReindexHandler(NexusDbContext db, EmbeddingLifecycle lifecycle, DocumentIndexer indexer,
    AccessService access, IOptions<KnowledgeOptions> options, KnowledgeWriteLock writes) : IBackgroundJobHandler
{
    public string Kind => "embedding-reindex";
    public async Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!(await access.ForUserAsync(job.OwnerId, ct)).Features.Any(x => x.Id == AdministrationConfiguration.Feature)) throw new ApiException(403, "admin_required", "重建全量索引需要管理權限。");
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
public sealed class EmbeddingBootstrapWorker(IServiceScopeFactory scopes, StorageReadiness readiness, IHostEnvironment environment, ILogger<EmbeddingBootstrapWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        var cleanedAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (readiness.Configured)
                {
                    using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                    var target = await scope.ServiceProvider.GetRequiredService<EmbeddingProfiles>().TargetAsync(stoppingToken);
                    var jobs = scope.ServiceProvider.GetRequiredService<JobService>();
                    var pending = await (from d in db.Set<KnowledgeDocument>() join r in db.Set<WorkspaceResource>() on d.Id equals r.Id
                        where !d.IsDeleted && d.CollectionId != null && d.Status == "reindex" && !db.Set<BackgroundJob>().Any(j => j.SubjectId == d.Id && j.ActiveKey != null)
                        select new { Document = d, r.OwnerId }).ToListAsync(stoppingToken);
                    foreach (var item in pending) { item.Document.Status = "indexing"; item.Document.JobId = jobs.Enqueue(item.OwnerId, item.Document.Id, item.Document.Id, "document-embedding", item.Document.FileName).Id; }
                    var settings = scope.ServiceProvider.GetRequiredService<IOptions<KnowledgeOptions>>().Value;
                    if (settings.AutoActivate && target.Status == "building")
                    {
                        var actor = await db.Set<UserRole>().Where(x => x.RoleId == "administrator").Select(x => (Guid?)x.UserId).FirstOrDefaultAsync(stoppingToken);
                        var subject = EmbeddingJobs.Subject(target.Id);
                        if (actor is Guid owner && !await db.Set<BackgroundJob>().AnyAsync(x => x.Kind == "embedding-reindex" && x.SubjectId == subject, stoppingToken)) jobs.Enqueue(owner, null, subject, "embedding-reindex", $"重建 {target.Model} 檢索索引");
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    if (DateTimeOffset.UtcNow - cleanedAt > TimeSpan.FromHours(1)) { await scope.ServiceProvider.GetRequiredService<EmbeddingLifecycle>().CleanupExpiredAsync(stoppingToken); cleanedAt = DateTimeOffset.UtcNow; }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("索引啟動或保留期限工作未完成。原因：{Reason}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
