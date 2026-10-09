using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class EmbeddingService(IEnumerable<IEmbeddingClient> clients, RetrievalInvocation invocations, EmbeddingBatchScheduler scheduler, IOptions<KnowledgeOptions> options)
{
    public bool Enabled => options.Value.EmbeddingProvider != "none";
    public string Profile => EmbeddingInput.Profile(options.Value);
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(Guid owner, EmbeddingProfile profile, IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken ct)
    {
        if (inputs.Count == 0) return [];
        using var lease = purpose == EmbeddingPurpose.Document && profile.Provider == "ollama" ? await scheduler.EnterAsync(ct) : null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            return await invocations.RunAsync(owner, "embedding", profile.Provider, profile.Model, async () => {
                var batch = await clients.Single(x => x.Provider == profile.Provider).EmbedBatchAsync(inputs, purpose, profile, timeout.Token);
                if (batch.Vectors.Count != inputs.Count) throw Invalid();
                foreach (var vector in batch.Vectors)
                {
                    if (vector.Length != profile.Dimensions || vector.Any(x => !float.IsFinite(x))) throw Invalid();
                    var norm = Math.Sqrt(vector.Sum(x => (double)x * x)); if (norm < 1e-12) throw Invalid();
                    for (var i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
                }
                return (batch.Vectors, batch.InputTokens);
            }, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "embedding_timeout", "語意索引服務逾時，請重試。"); }
    }
    private static ApiException Invalid() => new(502, "embedding_invalid", "向量回應數量、維度或數值不正確。");
}
