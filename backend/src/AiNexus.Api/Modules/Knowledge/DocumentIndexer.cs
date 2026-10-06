using AiNexus.BuildingBlocks;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Knowledge;

public sealed class DocumentIndexer(NexusDbContext db, EmbeddingProfiles profiles, EmbeddingService embeddings, EmbeddingVectorStore vectors,
    DocumentService documents, ITextChunker chunker, IOptions<KnowledgeOptions> options, KnowledgeWriteLock writes)
{
    public async Task IndexAsync(JobExecution execution, KnowledgeDocument document, EmbeddingProfile profile, CancellationToken ct)
    {
        var pages = await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == document.Id).OrderBy(x => x.PageNumber).ToListAsync(ct);
        var pieces = chunker.Chunk(pages);
        var contentCache = await vectors.CachedAsync(profile, pieces.Select(x => EmbeddingInput.Hash(EmbeddingInput.Document(document.FileName, x.HeadingPath, x.Text))).ToArray(), ct);
        await writes.Gate.WaitAsync(ct);
        try
        {
            var existing = await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == document.Id).OrderBy(x => x.Ordinal).ToListAsync(ct);
            var nextSearch = db.Database.IsSqlServer() ? 0 : (await db.Set<KnowledgeChunk>().Select(x => (int?)x.SearchId).MaxAsync(ct) ?? 0) + 1;
            var chunks = new List<KnowledgeChunk>();
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i]; var ordinal = i;
                var input = EmbeddingInput.Document(document.FileName, piece.HeadingPath, piece.Text); var hash = EmbeddingInput.Hash(input);
                var old = existing.FirstOrDefault(x => x.Ordinal == ordinal);
                if (old is not null && old.ContentHash.SequenceEqual(hash)) { old.StartPage = piece.StartPage; old.EndPage = piece.EndPage; chunks.Add(old); continue; }
                var chunk = new KnowledgeChunk { DocumentId = document.Id, Ordinal = ordinal, SearchId = nextSearch == 0 ? 0 : nextSearch++,
                    StartPage = piece.StartPage, EndPage = piece.EndPage, HeadingPath = piece.HeadingPath, Text = piece.Text, ContentHash = hash, TokenEstimate = piece.TokenEstimate };
                chunks.Add(chunk);
            }
            var retained = chunks.Select(x => x.Id).ToArray();
            if (profile.Status == "active") document.ChunkCount = chunks.Count;
            await execution.CheckpointAsync("結構化片段已保存", 0, chunks.Count, ct, async () => {
                await db.Set<KnowledgeChunk>().Where(x => x.DocumentId == document.Id && !retained.Contains(x.Id)).ExecuteDeleteAsync(ct);
                foreach (var old in existing.Where(x => !retained.Contains(x.Id))) db.Entry(old).State = EntityState.Detached;
                db.AddRange(chunks.Where(x => !existing.Any(e => e.Id == x.Id))); await db.SaveChangesAsync(ct);
            });
        }
        finally { writes.Gate.Release(); }
        if (profile.Provider == "none") return;
        var all = await db.Set<KnowledgeChunk>().AsNoTracking().Where(x => x.DocumentId == document.Id).OrderBy(x => x.Ordinal).ToListAsync(ct);
        var complete = (await vectors.ExistingAsync(profile, document.Id, ct)).ToHashSet(); var completed = complete.Count;
        foreach (var batch in all.Where(x => !complete.Contains(x.Id)).Chunk(options.Value.BatchSize))
        {
            await documents.RequireAsync(execution.Job.OwnerId, document.Id, ct, write: true);
            var cache = await vectors.CachedAsync(profile, batch.Select(x => x.ContentHash).ToArray(), ct);
            foreach (var chunk in batch) if (contentCache.TryGetValue(Convert.ToHexString(chunk.ContentHash), out var cached)) cache[Convert.ToHexString(chunk.ContentHash)] = cached;
            var missing = batch.GroupBy(x => Convert.ToHexString(x.ContentHash)).Where(x => !cache.ContainsKey(x.Key)).Select(x => x.First()).ToArray();
            if (missing.Length > 0)
            {
                var result = await embeddings.EmbedBatchAsync(execution.Job.OwnerId, profile, missing.Select(x => EmbeddingInput.Document(document.FileName, x.HeadingPath, x.Text)).ToArray(), EmbeddingPurpose.Document, ct);
                for (var i = 0; i < missing.Length; i++) cache[Convert.ToHexString(missing[i].ContentHash)] = result[i];
            }
            await documents.RequireAsync(execution.Job.OwnerId, document.Id, ct, write: true);
            var values = batch.Select(x => new EmbeddingWrite(x.Id, x.ContentHash, cache[Convert.ToHexString(x.ContentHash)])).ToArray();
            completed += batch.Length;
            await execution.CheckpointAsync("批次語意索引", completed, all.Count, ct, () => vectors.WriteBatchAsync(profile, values, ct));
        }
    }
    public async Task IndexCurrentAsync(JobExecution execution, KnowledgeDocument document, CancellationToken ct)
    {
        await profiles.TargetAsync(ct);
        var current = await db.Set<EmbeddingProfile>().Where(x => x.Status == "active" || x.Status == "building").OrderBy(x => x.Status == "active" ? 0 : 1).ToListAsync(ct);
        foreach (var profile in current) await IndexAsync(execution, document, profile, ct);
    }
}
