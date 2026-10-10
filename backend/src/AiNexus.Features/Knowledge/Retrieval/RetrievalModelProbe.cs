using AiNexus.Platform.Errors;
using AiNexus.Features.Inference;
using AiNexus.Platform.Diagnostics;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed record RetrievalConnectionDto(string Provider, string Model, string Endpoint, bool? Available, string Notice);

public sealed class RetrievalModelProbe(IEnumerable<IEmbeddingClient> embeddings, IEnumerable<IRerankClient> rerankers, EmbeddingService embeddingService,
    RerankService rerankService, IOptions<KnowledgeOptions> options, IOptions<InferenceOptions> inference, ILogger<RetrievalModelProbe> logger, Issues issues)
{
    public RetrievalConnectionDto Embedding => new(options.Value.EmbeddingProvider, options.Value.EmbeddingModel,
        options.Value.Endpoint.Length > 0 ? options.Value.Endpoint : options.Value.EmbeddingProvider == "google" ? "https://generativelanguage.googleapis.com" : inference.Value.BaseUrl,
        options.Value.EmbeddingProvider == "none" ? true : null, options.Value.EmbeddingProvider == "none" ? "未啟用向量，採用全文檢索。" : "尚未驗證模型與維度。");
    public RetrievalConnectionDto Rerank => new(options.Value.Rerank.Provider, options.Value.Rerank.Model, options.Value.Rerank.Endpoint,
        options.Value.Rerank.Provider == "none" ? true : null, options.Value.Rerank.Provider == "none" ? "未啟用重排。" : "尚未驗證重排服務。");
    public async Task<(RetrievalConnectionDto Embedding, RetrievalConnectionDto Rerank)> CheckAsync(Guid? actor, CancellationToken ct)
    {
        var settings = options.Value; var embedding = Embedding; var rerank = Rerank;
        if (settings.EmbeddingProvider != "none")
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            try
            {
                var profile = new EmbeddingProfile { Provider = settings.EmbeddingProvider, Model = settings.EmbeddingModel, Dimensions = settings.Dimensions, InputFormat = settings.InputFormat, QueryInstruction = settings.QueryInstruction };
                string[] input = ["合成索引測試 › 採購\n主管核准後辦理採購。", "合成索引測試 › 請假\n請假申請應完成簽核。"];
                var vectors = actor is Guid owner ? await embeddingService.EmbedBatchAsync(owner, profile, input, EmbeddingPurpose.Query, timeout.Token)
                    : (await embeddings.Single(x => x.Provider == profile.Provider).EmbedBatchAsync(input, EmbeddingPurpose.Query, profile, timeout.Token)).Vectors;
                if (vectors.Count != input.Length || vectors.Any(x => x.Length != settings.Dimensions || x.Any(v => !float.IsFinite(v)) || x.All(v => v == 0)))
                    throw new ExternalServiceException(Error.Upstream("embedding_probe_invalid"), "向量數量、維度或數值不符合設定。");
                embedding = embedding with { Available = true, Notice = $"批次向量化通過，維度 {settings.Dimensions}。" };
            }
            catch (Exception error) when (error is ExternalServiceException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            { ct.ThrowIfCancellationRequested(); using var logging = logger.BeginScope(new Dictionary<string, object?> { ["Stage"] = "embedding-probe", ["ExternalService"] = "embedding" });
                var issue = issues.Report(error, "embedding_probe_failed", LogLevel.Warning); embedding = embedding with { Available = false, Notice = Issues.Message(issue) }; }
        }
        if (settings.Rerank.Provider != "none")
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(settings.Rerank.TimeoutSeconds));
            try
            {
                string[] input = ["主管核准後辦理採購。", "請假申請應完成簽核。"];
                if (actor is Guid owner) await rerankService.RerankAsync(owner, "採購如何核准？", input.Select(x => new KnowledgeHitDto(Guid.Empty, "合成測試", 1, x, 0, Guid.Empty)).ToArray(), timeout.Token);
                else {
                    var scores = await rerankers.Single(x => x.Provider == settings.Rerank.Provider).RerankAsync("採購如何核准？", input, timeout.Token);
                    if (scores.Count != 2 || scores.Select(x => x.Index).Distinct().Count() != 2 || scores.Any(x => x.Index is < 0 or > 1 || !double.IsFinite(x.Score))) throw new ExternalServiceException(Error.Upstream("rerank_probe_invalid"), "重排回應無效。");
                }
                rerank = rerank with { Available = true, Notice = "合成查詢與候選重排通過。" };
            }
            catch (Exception error) when (error is ExternalServiceException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            { ct.ThrowIfCancellationRequested(); using var logging = logger.BeginScope(new Dictionary<string, object?> { ["Stage"] = "rerank-probe", ["ExternalService"] = "rerank" });
                var issue = issues.Report(error, "rerank_probe_failed", LogLevel.Warning); rerank = rerank with { Available = false, Notice = Issues.Message(issue) }; }
        }
        return (embedding, rerank);
    }
}
