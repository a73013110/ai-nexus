using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.BuildingBlocks;

namespace AiNexus.Modules.Knowledge;

public interface IRetrievalStore
{
    Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct);
}
public sealed class SqlServerRetrievalStore(IDbHelper<INexusDatabase> sql, IOptions<KnowledgeOptions> options) : IRetrievalStore
{
    public async Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct)
    {
        if (collections.Count == 0) return new("vector", []);
        if (vector is null) throw new ApiException(503, "fulltext_unavailable", "全文索引尚未就緒，請由管理員檢查 SQL 全文元件。");
        var statement = $"""
            WITH Authorized AS (
                SELECT c.[Id] AS [ChunkId], c.[DocumentId], d.[FileName] AS [Title], c.[StartPage] AS [PageNumber], c.[EndPage], c.[Ordinal], c.[HeadingPath], c.[Text]
                FROM [knowledge].[Chunks] c JOIN [knowledge].[Documents] d ON d.[Id] = c.[DocumentId]
                WHERE d.[CollectionId] IN @Collections AND d.[IsDeleted] = 0 AND d.[Status] = 'ready' AND c.[ProfileId] = @Profile
            )
            SELECT TOP (@Take) a.*, 1.0 - VECTOR_DISTANCE('cosine', e.[Vector], CAST(@Vector AS VECTOR({profile.Dimensions}))) AS [Score]
            FROM Authorized a JOIN {VectorDimensions.Table(profile.Dimensions)} e ON e.[ChunkId] = a.[ChunkId] AND e.[ProfileId] = @Profile
            ORDER BY VECTOR_DISTANCE('cosine', e.[Vector], CAST(@Vector AS VECTOR({profile.Dimensions}))), a.[ChunkId]
            """;
        var rows = await sql.QueryAsync<RetrievalRow>(statement, new { Take = options.Value.RerankCandidates, Collections = collections, Profile = profile.Id, Vector = JsonSerializer.Serialize(vector) }, commandTimeout: 30, cancellationToken: ct);
        return new("vector", rows.Where(x => x.Score >= options.Value.MinVectorScore).Select(x => x.Hit()).ToArray());
    }
}
public sealed partial class InMemoryRetrievalStore(NexusDbContext db, EmbeddingVectorStore vectors, IOptions<KnowledgeOptions> options) : IRetrievalStore
{
    public async Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct)
    {
        if (db.Database.IsSqlServer()) throw new InvalidOperationException("測試檢索儲存只供 SQLite 使用。");
        var rows = await (from c in db.Set<KnowledgeChunk>().AsNoTracking() join d in db.Set<KnowledgeDocument>() on c.DocumentId equals d.Id
            where d.CollectionId != null && collections.Contains(d.CollectionId.Value) && d.Status == "ready" && !d.IsDeleted && c.ProfileId == profile.Id
            select new RetrievalRow { ChunkId = c.Id, DocumentId = c.DocumentId, Title = d.FileName, PageNumber = c.StartPage, EndPage = c.EndPage, Ordinal = c.Ordinal, Text = c.Text, HeadingPath = c.HeadingPath }).ToListAsync(ct);
        var ids = rows.Select(x => x.ChunkId).ToArray();
        var stored = vector is null ? [] : await vectors.Rows(profile).Where(x => ids.Contains(x.ChunkId)).ToListAsync(ct);
        var map = stored.ToDictionary(x => x.ChunkId, x => x.Vector.Memory.ToArray()); var terms = Terms(query);
        foreach (var row in rows) row.Score = vector is null ? Lexical(terms, row.HeadingPath + "\n" + row.Text) : map.TryGetValue(row.ChunkId, out var value) ? Dot(vector, value) : -2;
        return new(vector is null ? "keyword" : "vector", rows.Where(x => vector is null ? x.Score > 0 : x.Score >= options.Value.MinVectorScore)
            .OrderByDescending(x => x.Score).ThenBy(x => x.ChunkId).Take(options.Value.RerankCandidates).Select(x => x.Hit()).ToArray());
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
    public static double Lexical(string[] terms, string text) => terms.Sum(term => Regex.Matches(text, Regex.Escape(term), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count);
    [GeneratedRegex(@"[\p{L}\p{N}_]{2,}", RegexOptions.CultureInvariant)] private static partial Regex WordPattern();
    [GeneratedRegex(@"[\p{IsCJKUnifiedIdeographs}]{2,}", RegexOptions.CultureInvariant)] private static partial Regex HanPattern();
}
public sealed class RetrievalRow
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = "";
    public int PageNumber { get; set; }
    public int EndPage { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = "";
    public string HeadingPath { get; set; } = "";
    public double Score { get; set; }
    public KnowledgeHitDto Hit() => new(DocumentId, Title, PageNumber, Text, Score, ChunkId, EndPage, Ordinal, VectorScore: Score, HeadingPath: HeadingPath);
}
