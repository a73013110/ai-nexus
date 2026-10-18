using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using System.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Features.Work.Knowledge.Embeddings;

namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed class RetrievalPipeline(AppDbContext db, RetrievalAuthorization authorization, EmbeddingProfiles profiles, QueryVectorCache cache,
    IRetrievalStore store, IQueryRewriter rewriter, RerankService reranker, IOptions<KnowledgeOptions> options, ILogger<RetrievalPipeline> logger)
{
    public async Task<Result<KnowledgeSearchDto>> SearchAsync(Guid actor, KnowledgeSearchRequest request, CancellationToken ct,
        IReadOnlyList<RewriteTurn>? history = null, string? mode = null, bool? rerank = null)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 2000) return KnowledgeErrors.QueryInvalid;
        var settings = options.Value; var requested = mode ?? settings.Retrieval.Mode;
        if (requested is not ("hybrid" or "keyword" or "vector")) return KnowledgeErrors.RetrievalModeInvalid;
        if (await authorization.CollectionsAsync(actor, request.CollectionIds, ct) is { IsSuccess: false } denied) return denied.Error;
        var profile = await profiles.ActiveAsync(ct); var actual = profile.Provider == "none" ? "keyword" : requested;
        if (actual != requested) RetrievalDiagnostics.Degraded(logger, requested, actual, "embeddings_disabled", service: "embedding");
        if (request.CollectionIds.Count == 0) return new KnowledgeSearchDto(actual, []);
        var query = request.Query.Trim(); var skippedRewrite = false; long rewriteMs = 0, embedMs = 0, searchMs = 0, rerankMs = 0;
        var timer = Stopwatch.StartNew();
        if (settings.QueryRewrite.Enabled && history?.Count > 0)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.QueryRewrite.TimeoutSeconds));
            try { query = await rewriter.RewriteAsync(actor, query, history.TakeLast(settings.QueryRewrite.MaxTurns * 2).ToArray(), timeout.Token); }
            catch (Exception error) when (error is ExternalServiceException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            { ct.ThrowIfCancellationRequested(); skippedRewrite = true; RetrievalDiagnostics.Degraded(logger, "rewrite", "original", "rewrite_skipped", error, "model"); }
            rewriteMs = timer.ElapsedMilliseconds;
        }
        timer.Restart();
        var vector = actual == "keyword" ? null : await cache.GetAsync(actor, profile, query, ct);
        embedMs = timer.ElapsedMilliseconds;
        timer.Restart(); var result = await store.SearchAsync(request.CollectionIds, profile, query, vector, actual, ct); searchMs = timer.ElapsedMilliseconds;
        // 權限在搜尋開始時讀一次；取得命中後、重排前（來源文字即將送往外部）、回傳前，各再檢查一次。
        // 遠端呼叫期間被撤銷的權限，必須在來源文字離開伺服器之前就被擋下。
        if (await authorization.HitsAsync(actor, result.Hits, ct, request.CollectionIds) is { IsSuccess: false } changed) return changed.Error;
        var hits = result.Hits; actual = result.Mode;
        if (rerank ?? settings.Rerank.Provider != "none")
        {
            timer.Restart();
            if (settings.Rerank.Provider != "none" && hits.Count > 0 && await authorization.CollectionsAsync(actor, request.CollectionIds, ct, fresh: true) is { IsSuccess: false } revoked) return revoked.Error;
            try
            {
                if (settings.Rerank.Provider == "none") throw new ExternalServiceException(Error.Unavailable("rerank_disabled"), "尚未啟用重排模型。");
                // 來源文字在此送往外部的重排服務；存取權剛才已檢查過。
                if (hits.Count > 0) hits = await reranker.RerankAsync(actor, query, hits, ct);
                actual += "+rerank";
            }
            catch (Exception error) when (error is ExternalServiceException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                ct.ThrowIfCancellationRequested();
                if (settings.Rerank.FailurePolicy == "fail") throw new ExternalServiceException(Error.Unavailable("rerank_unavailable"), "重排服務無法使用，請稍後重試。", error);
                actual += "(rerank-skipped)"; RetrievalDiagnostics.Degraded(logger, "rerank", "recall", "rerank_skipped", error, "rerank");
            }
            rerankMs = timer.ElapsedMilliseconds;
        }
        if (skippedRewrite) actual += "(rewrite-skipped)";
        if (await authorization.CollectionsAsync(actor, request.CollectionIds, ct, fresh: true) is { IsSuccess: false } final) return final.Error;
        if (await authorization.HitsAsync(actor, hits, ct, request.CollectionIds) is { IsSuccess: false } moved) return moved.Error;
        if (!await db.Set<EmbeddingProfile>().AsNoTracking().AnyAsync(x => x.Id == profile.Id && x.Status == "active", ct)) return KnowledgeErrors.ProfileChanged;
        return new KnowledgeSearchDto(actual, RetrievalRanking.Context(hits, settings.Retrieval), rewriteMs, embedMs, searchMs, rerankMs);
    }
}
