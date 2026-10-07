using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.BuildingBlocks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Data.SqlClient;
using AiNexus.BuildingBlocks.Diagnostics;

namespace AiNexus.Modules.Knowledge;

public interface IRetrievalStore
{
    Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct);
}
public sealed class SqlServerRetrievalStore(IDbHelper<INexusDatabase> sql, IOptions<KnowledgeOptions> options, IMemoryCache cache, ILogger<SqlServerRetrievalStore> logger) : IRetrievalStore
{
    public async Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct)
    {
        var fts = mode != "vector" && await cache.GetOrCreateAsync("knowledge-fulltext-ready", async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await sql.QuerySingleAsync<bool>("SELECT CAST(CASE WHEN SERVERPROPERTY('IsFullTextInstalled') = 1 AND EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028) AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks') AND is_enabled = 1) THEN 1 ELSE 0 END AS bit)", commandTimeout: 5, cancellationToken: ct);
        });
        if (!fts && mode == "keyword") throw new ApiException(503, "fulltext_unavailable", "全文索引尚未就緒，請由管理員檢查 SQL 全文元件。");
        if (!fts && mode == "hybrid") { mode = "vector"; RetrievalDiagnostics.Degraded(logger, "hybrid", "vector", "fulltext_not_ready"); }
        if (collections.Count == 0) return new(mode, []);
        var settings = options.Value;
        var vectorSql = vector is null ? "SELECT CAST(NULL AS uniqueidentifier) AS ChunkId, CAST(NULL AS float) AS VectorScore, CAST(NULL AS bigint) AS VectorRank WHERE 1 = 0" : $"""
            SELECT TOP (@VectorCandidates) a.[ChunkId], 1.0 - v.[Distance] AS [VectorScore], ROW_NUMBER() OVER (ORDER BY v.[Distance], a.[ChunkId]) AS [VectorRank]
            FROM Authorized a JOIN {VectorDimensions.Table(profile.Dimensions)} e ON e.[ChunkId] = a.[ChunkId] AND e.[ProfileId] = @Profile
            CROSS APPLY (SELECT VECTOR_DISTANCE('cosine', e.[Vector], CAST(@Vector AS VECTOR({profile.Dimensions}))) AS [Distance]) v
            WHERE @MinVectorScore = 0 OR 1.0 - v.[Distance] >= @MinVectorScore
            ORDER BY v.[Distance], a.[ChunkId]
            """;
        // FREETEXTTABLE's global top_n_by_rank would truncate before ACL. Rank authorized matches instead.
        var ftsSql = !fts ? "SELECT CAST(NULL AS uniqueidentifier) AS ChunkId, CAST(NULL AS bigint) AS FtsRank WHERE 1 = 0" : """
            SELECT TOP (@FtsCandidates) a.[ChunkId], ROW_NUMBER() OVER (ORDER BY f.[RANK] DESC, a.[ChunkId]) AS [FtsRank]
            FROM Authorized a JOIN FREETEXTTABLE([knowledge].[Chunks], ([Text], [HeadingPath]), @Query, LANGUAGE 1028) f ON f.[KEY] = a.[SearchId]
            ORDER BY f.[RANK] DESC, a.[ChunkId]
            """;
        var statement = $"""
            WITH Authorized AS (
                SELECT c.[Id] AS [ChunkId], c.[SearchId], c.[DocumentId], d.[FileName] AS [Title], c.[StartPage] AS [PageNumber], c.[EndPage], c.[Ordinal], c.[HeadingPath], c.[Text]
                FROM [knowledge].[Chunks] c JOIN [knowledge].[Documents] d ON d.[Id] = c.[DocumentId]
                WHERE d.[CollectionId] IN @Collections AND d.[IsDeleted] = 0 AND d.[Status] = 'ready'
            ), V AS ({vectorSql}), F AS ({ftsSql}), Combined AS (
                SELECT COALESCE(v.[ChunkId], f.[ChunkId]) AS [ChunkId], v.[VectorRank], f.[FtsRank], v.[VectorScore],
                    COALESCE(@VectorWeight / (@RrfK + v.[VectorRank]), 0) + COALESCE(@FtsWeight / (@RrfK + f.[FtsRank]), 0) AS [RrfScore]
                FROM V v FULL OUTER JOIN F f ON f.[ChunkId] = v.[ChunkId]
            )
            SELECT TOP (@Take) a.*, r.[VectorRank], r.[FtsRank], r.[VectorScore], r.[RrfScore], r.[RrfScore] AS [Score]
            FROM Combined r JOIN Authorized a ON a.[ChunkId] = r.[ChunkId]
            ORDER BY r.[RrfScore] DESC, a.[ChunkId]
            """;
        try
        {
            var rows = await sql.QueryAsync<RetrievalRow>(statement, new { Take = settings.RerankCandidates, Collections = collections, Profile = profile.Id, Vector = JsonSerializer.Serialize(vector), Query = query,
                settings.VectorCandidates, settings.FtsCandidates, settings.RrfK, settings.VectorWeight, settings.FtsWeight, settings.MinVectorScore }, commandTimeout: 30, cancellationToken: ct);
            return new(mode, rows.Select(x => x.Hit()).ToArray());
        }
        catch (SqlException error) when (fts && error.Number is 30010 or 30046 or 30053)
        {
            // Installed components can still fail at runtime (word breaker / FDHost).
            cache.Set("knowledge-fulltext-ready", false, TimeSpan.FromSeconds(30));
            if (mode == "hybrid" && vector is not null) {
                RetrievalDiagnostics.Degraded(logger, "hybrid", "vector", "fulltext_unavailable", error);
                return await SearchAsync(collections, profile, query, vector, "vector", ct);
            }
            throw new ApiException(503, "fulltext_unavailable", "全文搜尋服務目前無法使用。", error);
        }
    }
}
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
        var semantic = vector is null ? [] : rows.Where(x => x.VectorScore != null && (settings.MinVectorScore == 0 || x.VectorScore >= settings.MinVectorScore))
            .OrderByDescending(x => x.VectorScore).ThenBy(x => x.ChunkId).Take(settings.VectorCandidates).Select(x => x.Hit()).ToArray();
        var lexical = mode == "vector" ? [] : rows.Select(x => (Hit: x, Score: Lexical(terms, x.HeadingPath + "\n" + x.Text))).Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Hit.ChunkId).Take(settings.FtsCandidates).Select(x => x.Hit.Hit()).ToArray();
        return new(mode, RetrievalRanking.Fuse(semantic, lexical, settings));
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
    public int? VectorRank { get; set; }
    public int? FtsRank { get; set; }
    public double? VectorScore { get; set; }
    public double? RrfScore { get; set; }
    public KnowledgeHitDto Hit() => new(DocumentId, Title, PageNumber, Text, Score, ChunkId, EndPage, Ordinal, VectorRank, FtsRank, VectorScore, RrfScore, HeadingPath: HeadingPath);
}
