using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class NoneEmbeddingClient : IEmbeddingClient
{
    public string Provider => "none";
    public Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct)
        => throw new ExternalServiceException(Error.Conflict("embeddings_disabled"), "目前採用全文檢索，未啟用語意向量。");
}
