using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AiNexus.BuildingBlocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Knowledge;

public sealed partial class QueryVectorCache(EmbeddingService embeddings, IOptions<KnowledgeOptions> options) : IDisposable
{
    private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    public async Task<float[]> GetAsync(Guid actor, EmbeddingProfile profile, string query, CancellationToken ct)
    {
        var normalized = Whitespace().Replace(query.Normalize(NormalizationForm.FormKC).Trim(), " ");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized)); var key = (profile.Key, Convert.ToHexString(digest));
        if (options.Value.QueryCacheMinutes == 0) return (await embeddings.EmbedBatchAsync(actor, profile, [normalized], EmbeddingPurpose.Query, ct))[0];
        var gate = gates[digest[0] % gates.Length]; await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue<float[]>(key, out var vector)) return vector!;
            vector = (await embeddings.EmbedBatchAsync(actor, profile, [normalized], EmbeddingPurpose.Query, ct))[0];
            cache.Set(key, vector, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.QueryCacheMinutes) });
            return vector;
        }
        finally { gate.Release(); }
    }
    public void Dispose() { cache.Dispose(); foreach (var gate in gates) gate.Dispose(); }
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)] private static partial Regex Whitespace();
}
public sealed class RetrievalPipeline(NexusDbContext db, RetrievalAuthorization authorization, EmbeddingProfiles profiles, QueryVectorCache cache,
    IRetrievalStore store, IQueryRewriter rewriter, RerankService reranker, IOptions<KnowledgeOptions> options, ILogger<RetrievalPipeline> logger)
{
    public async Task<KnowledgeSearchDto> SearchAsync(Guid actor, KnowledgeSearchRequest request, CancellationToken ct,
        IReadOnlyList<RewriteTurn>? history = null, string? mode = null, bool? rerank = null)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 2000) throw new ApiException(400, "knowledge_query_invalid", "查詢需為 1 至 2000 個字元。");
        var settings = options.Value; var requested = mode ?? settings.Mode;
        if (requested is not ("hybrid" or "keyword" or "vector")) throw new ApiException(400, "retrieval_mode_invalid", "檢索模式不正確。");
        await authorization.CollectionsAsync(actor, request.CollectionIds, ct);
        var profile = await profiles.ActiveAsync(ct); var actual = profile.Provider == "none" ? "keyword" : requested;
        if (actual != requested) logger.LogWarning("未啟用向量模型，檢索模式由 {Requested} 改為 keyword。", requested);
        if (request.CollectionIds.Count == 0) return new(actual, []);
        var query = request.Query.Trim(); var skippedRewrite = false; long rewriteMs = 0, embedMs = 0, searchMs = 0, rerankMs = 0;
        var timer = Stopwatch.StartNew();
        if (settings.QueryRewrite.Enabled && history?.Count > 0)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.QueryRewrite.TimeoutSeconds));
            try { query = await rewriter.RewriteAsync(actor, query, history.TakeLast(settings.QueryRewrite.MaxTurns * 2).ToArray(), timeout.Token); logger.LogDebug("知識檢索獨立查詢：{Query}", query); }
            catch (Exception error) when (error is ApiException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            { ct.ThrowIfCancellationRequested(); skippedRewrite = true; logger.LogWarning("查詢改寫略過，使用原句。原因：{Reason}", error.GetType().Name); }
            rewriteMs = timer.ElapsedMilliseconds; await authorization.CollectionsAsync(actor, request.CollectionIds, ct);
        }
        timer.Restart();
        var vector = actual == "keyword" ? null : await cache.GetAsync(actor, profile, query, ct);
        embedMs = timer.ElapsedMilliseconds; await authorization.CollectionsAsync(actor, request.CollectionIds, ct);
        timer.Restart(); var result = await store.SearchAsync(request.CollectionIds, profile, query, vector, actual, ct); searchMs = timer.ElapsedMilliseconds;
        await authorization.HitsAsync(actor, result.Hits, ct);
        var hits = result.Hits; actual = result.Mode;
        if ((rerank ?? settings.Rerank.Provider != "none") && hits.Count > 0)
        {
            timer.Restart();
            try
            {
                if (settings.Rerank.Provider == "none") throw new ApiException(503, "rerank_disabled", "尚未啟用重排模型。");
                // Source text leaves the server here; access was checked immediately above.
                hits = await reranker.RerankAsync(actor, query, hits, ct); actual += "+rerank";
            }
            catch (Exception error) when (error is ApiException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                ct.ThrowIfCancellationRequested();
                if (settings.Rerank.FailurePolicy == "fail") throw new ApiException(503, "rerank_unavailable", "重排服務無法使用，請稍後重試。");
                actual += "(rerank-skipped)"; logger.LogWarning("重排略過，沿用召回排名。原因：{Reason}", error.GetType().Name);
            }
            rerankMs = timer.ElapsedMilliseconds;
        }
        if (skippedRewrite) actual += "(rewrite-skipped)";
        await authorization.CollectionsAsync(actor, request.CollectionIds, ct); await authorization.HitsAsync(actor, hits, ct);
        if (!await db.Set<EmbeddingProfile>().AsNoTracking().AnyAsync(x => x.Id == profile.Id && x.Status == "active", ct))
            throw new ApiException(409, "embedding_profile_changed", "檢索索引已切換，請重新送出提問。");
        return new(actual, RetrievalRanking.Context(hits, settings), rewriteMs, embedMs, searchMs, rerankMs);
    }
}
