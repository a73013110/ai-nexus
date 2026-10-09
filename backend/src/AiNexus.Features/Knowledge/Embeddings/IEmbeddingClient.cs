namespace AiNexus.Features.Knowledge.Embeddings;

public sealed record EmbeddingBatchResult(IReadOnlyList<float[]> Vectors, long? InputTokens = null);

public interface IEmbeddingClient
{
    string Provider { get; }
    Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct);
}
