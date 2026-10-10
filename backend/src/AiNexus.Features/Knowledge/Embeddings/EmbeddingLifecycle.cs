using System.Globalization;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;

namespace AiNexus.Features.Knowledge.Embeddings;

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

public sealed class EmbeddingLifecycle(NexusDbContext db, EmbeddingProfiles profiles, EmbeddingVectorStore vectors, KnowledgeWriteLock writes,
    JobService jobs, IOptions<KnowledgeOptions> options)
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
    /// <summary>
    /// Queues a rebuild of the profile, or returns the one already running. Like the other administrative changes below,
    /// the caller holds <see cref="KnowledgeWriteLock"/> and audits it; nothing is saved here.
    /// </summary>
    public async Task<Result<BackgroundJob>> QueueRebuildAsync(Guid actor, int id, CancellationToken ct)
    {
        var found = await RequireAsync(id, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        if (profile.Status == "retired") return KnowledgeErrors.ProfileRetired;
        var subject = EmbeddingJobs.Subject(id);
        return await db.Set<BackgroundJob>().SingleOrDefaultAsync(x => x.Kind == "embedding-reindex" && x.SubjectId == subject && x.ActiveKey != null, ct)
            ?? jobs.Enqueue(actor, null, subject, "embedding-reindex", $"重建 {profile.Model} 檢索索引");
    }
    public async Task<Result> ActivateCoreAsync(int id, CancellationToken ct)
    {
        var found = await RequireAsync(id, ct);
        if (!found.IsSuccess) return found.Error;
        var profile = found.Value;
        if (profile.Status == "active") return Result.Success;
        if (profile.Status != "building") return KnowledgeErrors.ProfileNotBuilding;
        if (!(await CoverageAsync(profile, ct)).Complete) return KnowledgeErrors.ProfileIncomplete;
        var now = DateTimeOffset.UtcNow;
        await db.Set<EmbeddingProfile>().Where(x => x.Status == "active").ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "retired").SetProperty(x => x.RetiredAt, now), ct);
        await db.Set<EmbeddingProfile>().Where(x => x.Id == id && x.Status == "building").ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "active").SetProperty(x => x.ActivatedAt, now).SetProperty(x => x.RetiredAt, (DateTimeOffset?)null), ct);
        return Result.Success;
    }
    public async Task<Result> ClearCoreAsync(int id, CancellationToken ct)
    {
        var found = await RequireAsync(id, ct);
        if (!found.IsSuccess) return found.Error;
        if (found.Value.Status != "retired") return KnowledgeErrors.ProfileNotRetired;
        await db.Set<ChunkEmbedding768>().Where(x => x.ProfileId == id).ExecuteDeleteAsync(ct);
        await db.Set<ChunkEmbedding1024>().Where(x => x.ProfileId == id).ExecuteDeleteAsync(ct);
        return Result.Success;
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
                    if (!(await ClearCoreAsync(profile.Id, ct)).IsSuccess) continue; db.AuditEvents.Add(new() { OwnerId = Guid.Empty, Action = "system.embedding_retention", Result = "cleared", DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { profile.Id }) });
                }
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<Result<EmbeddingProfile>> RequireAsync(int id, CancellationToken ct)
        => await db.Set<EmbeddingProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } profile ? profile : KnowledgeErrors.ProfileNotFound;
}
