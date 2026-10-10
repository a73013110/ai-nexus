using System.Text.Json;
using AiNexus.Features.Persistence;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Data.SqlClient;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class SqlServerRetrievalStore(ISqlDatabase<NexusDbContext> sql, IOptions<KnowledgeOptions> options, IMemoryCache cache, ILogger<SqlServerRetrievalStore> logger) : IRetrievalStore
{
    public async Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, EmbeddingProfile profile, string query, float[]? vector, string mode, CancellationToken ct)
    {
        var fts = mode != "vector" && await cache.GetOrCreateAsync("knowledge-fulltext-ready", async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await sql.QuerySingleAsync<bool>("SELECT CAST(CASE WHEN SERVERPROPERTY('IsFullTextInstalled') = 1 AND EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028) AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks') AND is_enabled = 1) THEN 1 ELSE 0 END AS bit)", commandTimeout: 5, cancellationToken: ct);
        });
        if (!fts && mode == "keyword") throw new ExternalServiceException(Error.Unavailable("fulltext_unavailable"), "全文索引尚未就緒，請由管理員檢查 SQL 全文元件。");
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
            var rows = await sql.QueryAsync<RetrievalRow>(statement, new { Take = settings.Retrieval.RerankCandidates, Collections = collections, Profile = profile.Id, Vector = JsonSerializer.Serialize(vector), Query = query,
                settings.Retrieval.VectorCandidates, settings.Retrieval.FtsCandidates, settings.Retrieval.RrfK, settings.Retrieval.VectorWeight, settings.Retrieval.FtsWeight, settings.Retrieval.MinVectorScore }, commandTimeout: 30, cancellationToken: ct);
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
            throw new ExternalServiceException(Error.Unavailable("fulltext_unavailable"), "全文搜尋服務目前無法使用。", error);
        }
    }
}
