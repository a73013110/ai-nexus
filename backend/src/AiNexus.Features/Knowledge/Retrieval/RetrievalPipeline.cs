using System.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

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
        if (actual != requested) RetrievalDiagnostics.Degraded(logger, requested, actual, "embeddings_disabled", service: "embedding");
        if (request.CollectionIds.Count == 0) return new(actual, []);
        var query = request.Query.Trim(); var skippedRewrite = false; long rewriteMs = 0, embedMs = 0, searchMs = 0, rerankMs = 0;
        var timer = Stopwatch.StartNew();
        if (settings.QueryRewrite.Enabled && history?.Count > 0)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.QueryRewrite.TimeoutSeconds));
            try { query = await rewriter.RewriteAsync(actor, query, history.TakeLast(settings.QueryRewrite.MaxTurns * 2).ToArray(), timeout.Token); }
            catch (Exception error) when (error is ApiException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            { ct.ThrowIfCancellationRequested(); skippedRewrite = true; RetrievalDiagnostics.Degraded(logger, "rewrite", "original", "rewrite_skipped", error, "model"); }
            rewriteMs = timer.ElapsedMilliseconds;
        }
        timer.Restart();
        var vector = actual == "keyword" ? null : await cache.GetAsync(actor, profile, query, ct);
        embedMs = timer.ElapsedMilliseconds;
        timer.Restart(); var result = await store.SearchAsync(request.CollectionIds, profile, query, vector, actual, ct); searchMs = timer.ElapsedMilliseconds;
        // Collection access is read once per search, then again only after a remote call and before any source text leaves:
        // before reranking sends it out, and before the result is returned.
        await authorization.HitsAsync(actor, result.Hits, ct, request.CollectionIds);
        var hits = result.Hits; actual = result.Mode;
        if (rerank ?? settings.Rerank.Provider != "none")
        {
            timer.Restart();
            if (settings.Rerank.Provider != "none" && hits.Count > 0) await authorization.CollectionsAsync(actor, request.CollectionIds, ct, fresh: true);
            try
            {
                if (settings.Rerank.Provider == "none") throw new ApiException(503, "rerank_disabled", "尚未啟用重排模型。");
                // Source text leaves the server here; access was checked immediately above.
                if (hits.Count > 0) hits = await reranker.RerankAsync(actor, query, hits, ct);
                actual += "+rerank";
            }
            catch (Exception error) when (error is ApiException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                ct.ThrowIfCancellationRequested();
                if (settings.Rerank.FailurePolicy == "fail") throw new ApiException(503, "rerank_unavailable", "重排服務無法使用，請稍後重試。", error);
                actual += "(rerank-skipped)"; RetrievalDiagnostics.Degraded(logger, "rerank", "recall", "rerank_skipped", error, "rerank");
            }
            rerankMs = timer.ElapsedMilliseconds;
        }
        if (skippedRewrite) actual += "(rewrite-skipped)";
        await authorization.CollectionsAsync(actor, request.CollectionIds, ct, fresh: true); await authorization.HitsAsync(actor, hits, ct, request.CollectionIds);
        if (!await db.Set<EmbeddingProfile>().AsNoTracking().AnyAsync(x => x.Id == profile.Id && x.Status == "active", ct))
            throw new ApiException(409, "embedding_profile_changed", "檢索索引已切換，請重新送出提問。");
        return new(actual, RetrievalRanking.Context(hits, settings), rewriteMs, embedMs, searchMs, rerankMs);
    }
}
