using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Admin.AccessControl;
using System.Globalization;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Work.Jobs;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed record EmbeddingCoverageDto(int CompletedChunks, int TotalChunks, int PendingDocuments)
{
    public bool Complete => CompletedChunks == TotalChunks && PendingDocuments == 0;
    public double Ratio => TotalChunks == 0 ? (PendingDocuments == 0 ? 1 : 0) : (double)CompletedChunks / TotalChunks;
}

public static class EmbeddingJobs
{
    public static Guid Subject(int profile) => new($"{profile:x8}-bfac-4dc0-a11a-000000000000");
    public static int Profile(Guid subject) => int.Parse(subject.ToString("N")[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

public sealed class EmbeddingLifecycle(AppDbContext db, EmbeddingProfiles profiles, EmbeddingVectorStore vectors,
    JobService jobs, IOptions<KnowledgeOptions> options, TimeProvider clock, AuditTrail audit)
{
    private IQueryable<Document> Documents => db.Documents.Where(x => x.CollectionId != null && !x.IsDeleted);
    public async Task<EmbeddingCoverageDto> CoverageAsync(EmbeddingProfile profile, CancellationToken ct)
    {
        var chunks = from c in db.Chunks join d in Documents on c.DocumentId equals d.Id select c;
        var total = await chunks.CountAsync(ct);
        var completed = profile.Provider == "none" ? total : await (from e in vectors.Rows(profile) join c in chunks on e.ChunkId equals c.Id select e.ChunkId).CountAsync(ct);
        return new(completed, total, await Documents.CountAsync(x => x.Status != "ready", ct));
    }
    /// <summary>
    /// 啟用前的完整性判斷。RCSI 每一句各取一次快照，<see cref="CoverageAsync"/> 的三個計數可能跨過別的文件寫入而剛好相等，所以這裡不比計數：
    /// 兩句存在性查詢各自成立就是事實。之後才開始重新索引的文件不影響判斷，它們的工作會補上這個模型的向量。
    /// </summary>
    private async Task<bool> CoveredAsync(EmbeddingProfile profile, CancellationToken ct)
    {
        if (profile.Provider != "none")
        {
            var rows = vectors.Rows(profile);
            if (await (from c in db.Chunks join d in Documents on c.DocumentId equals d.Id select c).AnyAsync(c => !rows.Any(e => e.ChunkId == c.Id), ct)) return false;
        }
        return !await Documents.AnyAsync(x => x.Status != "ready", ct);
    }
    public async Task<IReadOnlyList<EmbeddingProfileDto>> ListAsync(CancellationToken ct)
    {
        await profiles.TargetAsync(ct);
        var all = await db.EmbeddingProfiles.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(ct);
        var tasks = await db.BackgroundJobs.AsNoTracking().Where(x => x.Kind == EmbeddingReindexHandler.JobKind || x.Kind == EmbeddingClearHandler.JobKind).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var result = new List<EmbeddingProfileDto>();
        foreach (var p in all) result.Add(new(p.Id, p.Key, p.Provider, p.Model, p.Dimensions, p.Status, p.CreatedAt, p.ActivatedAt, p.RetiredAt,
            await CoverageAsync(p, ct), tasks.FirstOrDefault(x => x.SubjectId == EmbeddingJobs.Subject(p.Id)) is { } job ? JobService.Describe(job) : null));
        return result;
    }
    /// <summary>
    /// 把向量模型的重建排入佇列；若已有進行中的重建，直接回傳它。與下方其他管理變更相同，呼叫端要在
    /// <see cref="AdministrativeWrite"/> 內呼叫（已持有 <see cref="KnowledgeLocks.EmbeddingProfile"/> 並會稽核），這裡不儲存任何變更。
    /// </summary>
    public async Task<Result<BackgroundJob>> QueueRebuildAsync(Guid actor, int id, CancellationToken ct)
    {
        var found = await RequireAsync(id, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        if (profile.Status == "retired") return KnowledgeErrors.ProfileRetired;
        var subject = EmbeddingJobs.Subject(id);
        return await db.BackgroundJobs.SingleOrDefaultAsync(x => x.Kind == EmbeddingReindexHandler.JobKind && x.SubjectId == subject && x.ActiveKey != null, ct)
            ?? jobs.Enqueue(actor, null, subject, EmbeddingReindexHandler.JobKind, $"重建 {profile.Model} 檢索索引");
    }
    public async Task<Result> ActivateCoreAsync(int id, CancellationToken ct)
    {
        var found = await RequireAsync(id, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        if (profile.Status == "active") return Result.Success;
        if (profile.Status != "building") return KnowledgeErrors.ProfileNotBuilding;
        if (!await CoveredAsync(profile, ct)) return KnowledgeErrors.ProfileIncomplete;
        var now = clock.GetUtcNow();
        await db.EmbeddingProfiles.Where(x => x.Status == "active").ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "retired").SetProperty(x => x.RetiredAt, now), ct);
        await db.EmbeddingProfiles.Where(x => x.Id == id && x.Status == "building").ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "active").SetProperty(x => x.ActivatedAt, now).SetProperty(x => x.RetiredAt, (DateTimeOffset?)null), ct);
        return Result.Success;
    }
    /// <summary>把清除退役向量排入佇列；若已有進行中的清除，直接回傳它。規則同 <see cref="QueueRebuildAsync"/>，實際刪除見 <see cref="EmbeddingClearHandler"/>。</summary>
    public async Task<Result<BackgroundJob>> QueueClearAsync(Guid actor, int id, CancellationToken ct)
    {
        var found = await RequireAsync(id, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        if (profile.Status != "retired") return KnowledgeErrors.ProfileNotRetired;
        var subject = EmbeddingJobs.Subject(id);
        return await db.BackgroundJobs.SingleOrDefaultAsync(x => x.Kind == EmbeddingClearHandler.JobKind && x.SubjectId == subject && x.ActiveKey != null, ct)
            ?? jobs.Enqueue(actor, null, subject, EmbeddingClearHandler.JobKind, $"清除 {profile.Model} 退役向量");
    }
    /// <summary>清除保留期已過的退役向量。由背景工作者呼叫，所以直接分批刪完；每批各自提交、不持鎖，理由同 <see cref="EmbeddingClearHandler"/>。</summary>
    public async Task CleanupExpiredAsync(CancellationToken ct)
    {
        var expired = (await db.EmbeddingProfiles.AsNoTracking().Where(x => x.Status == "retired" && x.RetiredAt != null).ToListAsync(ct))
            .Where(x => x.RetiredAt < clock.GetUtcNow().AddDays(-options.Value.Embedding.RetiredRetentionDays)).ToArray();
        foreach (var profile in expired)
        {
            var cleared = 0;
            int deleted;
            do { deleted = await vectors.DeleteRetiredBatchAsync(profile, ct); cleared += deleted; } while (deleted > 0);
            if (cleared == 0) continue;
            audit.Record(KnowledgeAudit.ProfileCleared, Guid.Empty, details: new { profileId = profile.Id, vectors = cleared, automatic = true });
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task<Result<EmbeddingProfile>> RequireAsync(int id, CancellationToken ct)
        => await db.EmbeddingProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } profile ? profile : KnowledgeErrors.ProfileNotFound;
}
