using System.Text.RegularExpressions;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed partial class InMemoryRetrievalStore(NexusDbContext db, EmbeddingVectorStore vectors, IOptions<KnowledgeOptions> options) : IRetrievalStore
{
    public async Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct)
    {
        if (db.Database.IsSqlServer()) throw new InvalidOperationException("測試檢索儲存只供 SQLite 使用。");
        var rows = await (from c in db.Set<KnowledgeChunk>().AsNoTracking() join d in db.Set<KnowledgeDocument>() on c.DocumentId equals d.Id
            where d.CollectionId != null && collections.Contains(d.CollectionId.Value) && d.Status == "ready" && !d.IsDeleted
            select new RetrievalRow { ChunkId = c.Id, DocumentId = c.DocumentId, Title = d.FileName, PageNumber = c.StartPage, EndPage = c.EndPage, Ordinal = c.Ordinal, Text = c.Text, HeadingPath = c.HeadingPath }).ToListAsync(ct);
        var ids = rows.Select(x => x.ChunkId).ToArray();
        var stored = vector is null ? [] : await vectors.Rows(profile).Where(x => ids.Contains(x.ChunkId)).ToListAsync(ct);
        var map = stored.ToDictionary(x => x.ChunkId, x => x.Vector.Memory.ToArray()); var terms = Terms(query);
        foreach (var row in rows) row.VectorScore = vector is not null && map.TryGetValue(row.ChunkId, out var value) ? Dot(vector, value) : null;
        var settings = options.Value;
        var semantic = vector is null ? [] : rows.Where(x => x.VectorScore != null && (settings.Retrieval.MinVectorScore == 0 || x.VectorScore >= settings.Retrieval.MinVectorScore))
            .OrderByDescending(x => x.VectorScore).ThenBy(x => x.ChunkId).Take(settings.Retrieval.VectorCandidates).Select(x => x.Hit()).ToArray();
        var lexical = mode == "vector" ? [] : rows.Select(x => (Hit: x, Score: Lexical(terms, x.HeadingPath + "\n" + x.Text))).Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Hit.ChunkId).Take(settings.Retrieval.FtsCandidates).Select(x => x.Hit.Hit()).ToArray();
        return new(mode, RetrievalRanking.Fuse(semantic, lexical, settings.Retrieval));
    }
    public static double Dot(float[] query, float[] stored)
    {
        if (query.Length != stored.Length) return -2;
        double score = 0; for (var i = 0; i < query.Length; i++) score += (double)query[i] * stored[i]; return Math.Clamp(score, -1, 1);
    }
    public static string[] Terms(string query)
    {
        var words = WordPattern().Matches(query.ToLowerInvariant()).Select(x => x.Value).ToList();
        foreach (var part in HanPattern().Matches(query).Select(x => x.Value))
            for (var i = 0; i < part.Length - 1; i++) words.Add(part.Substring(i, 2));
        return words.Distinct().Take(128).ToArray();
    }
    public static double Lexical(string[] terms, string text) => terms.Sum(term => Regex.Count(text, Regex.Escape(term), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    [GeneratedRegex(@"[\p{L}\p{N}_]{2,}", RegexOptions.CultureInvariant)] private static partial Regex WordPattern();
    [GeneratedRegex(@"[\p{IsCJKUnifiedIdeographs}]{2,}", RegexOptions.CultureInvariant)] private static partial Regex HanPattern();
}
