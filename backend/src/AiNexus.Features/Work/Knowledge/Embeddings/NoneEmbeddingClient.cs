using AiNexus.Data.App.Knowledge;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

public sealed class NoneEmbeddingClient : IEmbeddingClient
{
    public Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct)
        => throw new ExternalServiceException(KnowledgeErrors.EmbeddingsDisabled, "目前採用全文檢索，未啟用語意向量。");
}
