using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Indexing;

public sealed class DocumentIndexer(NexusDbContext db, EmbeddingProfiles profiles, EmbeddingService embeddings, EmbeddingVectorStore vectors,
    DocumentService documents, ITextChunker chunker, IOptions<KnowledgeOptions> options, KnowledgeWriteLock writes, JobService jobs)
{
    private async Task<List<EmbeddingProfile>> CurrentAsync(CancellationToken ct)
    {
        await profiles.TargetAsync(ct);
        return await db.Set<EmbeddingProfile>().AsNoTracking().Where(x => x.Status == "active" || x.Status == "building").OrderBy(x => x.Status == "active" ? 0 : 1).ToListAsync(ct);
    }
    public async Task<Result> PrepareAsync(JobExecution execution, KnowledgeDocument document, CancellationToken ct)
    {
        var current = await CurrentAsync(ct);
        var pages = await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == document.Id).OrderBy(x => x.PageNumber).ToListAsync(ct);
        var chunked = chunker.Chunk(pages);
        if (!chunked.IsSuccess) return chunked.Error;
        var pieces = chunked.Value;
        if (pieces.Count == 0) return KnowledgeErrors.DocumentHasNoText;
        var hashes = pieces.Select(x => EmbeddingInput.Hash(EmbeddingInput.Document(document.FileName, x.HeadingPath, x.Text))).ToArray();
        var cached = new Dictionary<int, Dictionary<string, float[]>>();
        foreach (var profile in current.Where(x => x.Provider != "none")) cached[profile.Id] = await vectors.CachedAsync(profile, hashes, ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            var existing = await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == document.Id).OrderBy(x => x.Ordinal).ToListAsync(ct);
            var nextSearch = db.Database.IsSqlServer() ? 0 : (await db.Set<KnowledgeChunk>().Select(x => (int?)x.SearchId).MaxAsync(ct) ?? 0) + 1;
            var chunks = new List<KnowledgeChunk>();
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i]; var old = existing.FirstOrDefault(x => x.Ordinal == i);
                if (old is not null && old.ContentHash.SequenceEqual(hashes[i])) { old.StartPage = piece.StartPage; old.EndPage = piece.EndPage; chunks.Add(old); continue; }
                chunks.Add(new() { DocumentId = document.Id, Ordinal = i, SearchId = nextSearch == 0 ? 0 : nextSearch++,
                    StartPage = piece.StartPage, EndPage = piece.EndPage, HeadingPath = piece.HeadingPath, Text = piece.Text, ContentHash = hashes[i], TokenEstimate = piece.TokenEstimate });
            }
            var retained = chunks.Select(x => x.Id).ToArray(); document.ChunkCount = chunks.Count; document.Status = "indexing";
            await execution.CheckpointAsync("結構化片段已保存", 0, chunks.Count, ct, async () => {
                await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == document.Id && !retained.Contains(x.Id)).ExecuteDeleteAsync(ct);
                foreach (var old in existing.Where(x => !retained.Contains(x.Id))) db.Entry(old).State = EntityState.Detached;
                db.AddRange(chunks.Where(x => !existing.Any(e => e.Id == x.Id))); await db.SaveChangesAsync(ct);
                // Copy cached vectors before the old chunk rows disappear across job boundaries.
                foreach (var profile in current.Where(x => x.Provider != "none"))
                {
                    var copy = chunks.Where(x => cached[profile.Id].ContainsKey(Convert.ToHexString(x.ContentHash)))
                        .Select(x => new EmbeddingWrite(x.Id, x.ContentHash, cached[profile.Id][Convert.ToHexString(x.ContentHash)]));
                    foreach (var batch in copy.Chunk(options.Value.Embedding.BatchSize)) await vectors.WriteBatchAsync(profile, batch, ct);
                }
            });
        }
        finally { writes.Gate.Release(); }
        return Result.Success;
    }
    public async Task<Result> QueueAsync(JobExecution execution, KnowledgeDocument document, CancellationToken ct)
    {
        var prepared = await PrepareAsync(execution, document, ct);
        if (!prepared.IsSuccess) return prepared;
        document.JobId = jobs.Enqueue(execution.Job.OwnerId, document.Id, document.Id, "document-embedding", document.FileName).Id;
        await execution.CheckpointAsync("等待批次向量化", 0, document.ChunkCount, ct);
        return Result.Success;
    }
    private async Task<Result> EmbedAsync(JobExecution execution, KnowledgeDocument document, EmbeddingProfile profile, Guid actor, CancellationToken ct)
    {
        if (profile.Provider == "none") return Result.Success;
        var version = document.TextVersion;
        var all = await db.Set<KnowledgeChunk>().AsNoTracking().Where(x => x.DocumentId == document.Id).OrderBy(x => x.Ordinal).ToListAsync(ct);
        var complete = (await vectors.ExistingAsync(profile, document.Id, ct)).ToHashSet(); var completed = complete.Count;
        foreach (var batch in all.Where(x => !complete.Contains(x.Id)).Chunk(options.Value.Embedding.BatchSize))
        {
            if (await RequireUnchangedAsync(actor, document, profile, version, ct) is { IsSuccess: false } changed) return changed;
            var cache = await vectors.CachedAsync(profile, batch.Select(x => x.ContentHash).ToArray(), ct);
            var missing = batch.GroupBy(x => Convert.ToHexString(x.ContentHash)).Where(x => !cache.ContainsKey(x.Key)).Select(x => x.First()).ToArray();
            if (missing.Length > 0)
            {
                var result = await embeddings.EmbedBatchAsync(actor, profile, missing.Select(x => EmbeddingInput.Document(document.FileName, x.HeadingPath, x.Text)).ToArray(), EmbeddingPurpose.Document, ct);
                for (var i = 0; i < missing.Length; i++) cache[Convert.ToHexString(missing[i].ContentHash)] = result[i];
            }
            if (await RequireUnchangedAsync(actor, document, profile, version, ct) is { IsSuccess: false } moved) return moved;
            var values = batch.Select(x => new EmbeddingWrite(x.Id, x.ContentHash, cache[Convert.ToHexString(x.ContentHash)])).ToArray(); completed += batch.Length;
            await execution.CheckpointAsync("批次語意索引", completed, all.Count, ct, () => vectors.WriteBatchAsync(profile, values, ct));
        }
        return Result.Success;
    }
    private async Task<Result> RequireUnchangedAsync(Guid actor, KnowledgeDocument document, EmbeddingProfile profile, int version, CancellationToken ct)
    {
        var allowed = await documents.RequireAsync(actor, document.Id, ct, write: true);
        if (!allowed.IsSuccess) return allowed.Error;
        if (!await db.Set<KnowledgeDocument>().AsNoTracking().AnyAsync(x => x.Id == document.Id && x.TextVersion == version && !x.IsDeleted, ct)
            || !await db.Set<EmbeddingProfile>().AsNoTracking().AnyAsync(x => x.Id == profile.Id && (x.Status == "active" || x.Status == "building"), ct))
            return KnowledgeErrors.IndexSourceChanged;
        return Result.Success;
    }
    private async Task ReadyAsync(JobExecution execution, KnowledgeDocument document, CancellationToken ct)
    {
        document.Status = "ready";
        (await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).SingleAsync(x => x.Id == document.Id, ct)).UpdatedAt = db.Clock.GetUtcNow();
        await execution.CheckpointAsync("使用中索引已完成", document.ChunkCount, document.ChunkCount, ct);
    }
    public async Task<Result> IndexCurrentAsync(JobExecution execution, KnowledgeDocument document, CancellationToken ct)
    {
        var prepared = await PrepareAsync(execution, document, ct);
        if (!prepared.IsSuccess) return prepared;
        foreach (var profile in await CurrentAsync(ct))
        {
            var embedded = await EmbedAsync(execution, document, profile, execution.Job.OwnerId, ct);
            if (!embedded.IsSuccess) return embedded;
            if (profile.Status == "active") await ReadyAsync(execution, document, ct);
        }
        return Result.Success;
    }
    public async Task<Result> IndexAsync(JobExecution execution, KnowledgeDocument document, EmbeddingProfile target, CancellationToken ct)
    {
        // Reindex uses saved pages, under the source owner's ACL and quota, without reopening OCR.
        var owner = await db.Set<WorkspaceResource>().IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.Id == document.Id).Select(x => x.OwnerId).SingleAsync(ct);
        var allowed = await documents.RequireAsync(owner, document.Id, ct, write: true);
        if (!allowed.IsSuccess) return allowed.Error;
        var prepared = await PrepareAsync(execution, document, ct);
        if (!prepared.IsSuccess) return prepared;
        var active = await profiles.ActiveAsync(ct);
        var embedded = await EmbedAsync(execution, document, active, owner, ct);
        if (!embedded.IsSuccess) return embedded;
        await ReadyAsync(execution, document, ct);
        return target.Id != active.Id ? await EmbedAsync(execution, document, target, owner, ct) : Result.Success;
    }
}
