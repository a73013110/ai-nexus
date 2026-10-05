using System.Text.Json;
using System.Text.RegularExpressions;
using AiNexus.BuildingBlocks;
using AiNexus.Database;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Knowledge;

public sealed partial class NativeVectorStore(NexusDbContext db, IDbHelper<INexusDatabase> sql, IOptions<KnowledgeOptions> options)
{
    private bool? native;
    private int Dimensions => options.Value.Dimensions == 1024 ? 1024 : 768;
    private string Column => Dimensions == 1024 ? "EmbeddingVector1024" : "EmbeddingVector";
    private async Task<bool> NativeAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || !options.Value.UseNativeVector) return false;
        return native ??= await sql.QuerySingleAsync<int>("SELECT CASE WHEN COL_LENGTH('knowledge.Chunks', @Column) IS NOT NULL THEN 1 ELSE 0 END", new { Column }, commandTimeout: 5, cancellationToken: ct) == 1;
    }
    public async Task WriteAsync(Guid id, string? vector, CancellationToken ct)
    {
        if (vector is not null && await NativeAsync(ct))
        {
            if (options.Value.Dimensions == 1024)
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [knowledge].[Chunks] SET [EmbeddingVector1024] = CAST({vector} AS VECTOR(1024)) WHERE [Id] = {id}", ct);
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [knowledge].[Chunks] SET [EmbeddingVector] = CAST({vector} AS VECTOR(768)) WHERE [Id] = {id}", ct);
        }
    }
    public async Task<KnowledgeSearchDto> SearchAsync(IReadOnlyList<Guid> collections, string query, float[]? vector, string profile, CancellationToken ct)
    {
        if (collections.Count == 0) return new(vector is null ? "keyword" : "vector", []);
        if (vector is not null && await NativeAsync(ct))
        {
            // ACL-approved collection IDs are in the WHERE predicate before TOP/distance.
            var statement = $"""
                SELECT TOP (@Take) c.[DocumentId], d.[FileName] AS [Title], c.[PageNumber], c.[Text],
                    1.0 - VECTOR_DISTANCE('cosine', c.[{Column}], CAST(@Vector AS VECTOR({Dimensions}))) AS [Score]
                FROM [knowledge].[Chunks] c
                INNER JOIN [knowledge].[Documents] d ON d.[Id] = c.[DocumentId]
                WHERE d.[CollectionId] IN @Collections AND d.[IsDeleted] = 0 AND d.[Status] = 'ready'
                    AND c.[EmbeddingProfile] = @Profile AND c.[{Column}] IS NOT NULL
                ORDER BY VECTOR_DISTANCE('cosine', c.[{Column}], CAST(@Vector AS VECTOR({Dimensions}))), c.[Id]
                """;
            var hits = await sql.QueryAsync<KnowledgeHitDto>(statement, new { Take = options.Value.TopK, Collections = collections, Vector = JsonSerializer.Serialize(vector), Profile = profile }, commandTimeout: 10, cancellationToken: ct);
            return new("sql-vector", hits.Where(x => x.Score > .1).ToList());
        }
        var candidates = from chunk in db.Set<KnowledgeChunk>().AsNoTracking() join doc in db.Set<KnowledgeDocument>() on chunk.DocumentId equals doc.Id
            where doc.CollectionId != null && collections.Contains(doc.CollectionId.Value) && !doc.IsDeleted && doc.Status == "ready" && (vector == null || chunk.EmbeddingProfile == profile)
            select new { chunk.DocumentId, Title = doc.FileName, chunk.PageNumber, chunk.Text, chunk.EmbeddingJson };
        if (await candidates.CountAsync(ct) > options.Value.PortableCandidateLimit)
            throw new ApiException(413, "knowledge_scope_too_large", "此範圍超過可攜式檢索上限，請縮小知識庫範圍，或由管理員啟用 SQL 原生向量欄位。");
        var rows = await candidates.ToListAsync(ct); var terms = Terms(query);
        var scored = rows.Select(x => new KnowledgeHitDto(x.DocumentId, x.Title, x.PageNumber, x.Text,
            vector is null ? Lexical(terms, x.Text) : Cosine(vector, x.EmbeddingJson))).Where(x => x.Score > (vector is null ? 0 : .1)).OrderByDescending(x => x.Score).ThenBy(x => x.DocumentId).Take(options.Value.TopK).ToList();
        return new(vector is null ? "keyword" : "portable-vector", scored);
    }
    private static double Cosine(float[] query, string? stored)
    {
        if (stored is null) return 0;
        var value = JsonSerializer.Deserialize<float[]>(stored);
        if (value?.Length != query.Length) return 0;
        double score = 0; for (var i = 0; i < query.Length; i++) score += (double)query[i] * value[i]; return score;
    }
    private static string[] Terms(string query)
    {
        var words = WordPattern().Matches(query.ToLowerInvariant()).Select(x => x.Value).ToList();
        foreach (var part in HanPattern().Matches(query).Select(x => x.Value))
            for (var i = 0; i < part.Length - 1; i++) words.Add(part.Substring(i, 2));
        return words.Distinct().Take(128).ToArray();
    }
    private static double Lexical(string[] terms, string text)
    {
        var lower = text.ToLowerInvariant(); return terms.Length == 0 ? 0 : (double)terms.Count(x => lower.Contains(x, StringComparison.Ordinal)) / terms.Length;
    }
    [GeneratedRegex(@"[\p{L}\p{N}_]{2,}", RegexOptions.CultureInvariant)] private static partial Regex WordPattern();
    [GeneratedRegex(@"[\p{IsCJKUnifiedIdeographs}]{2,}", RegexOptions.CultureInvariant)] private static partial Regex HanPattern();
}
