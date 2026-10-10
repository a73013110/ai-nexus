using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

public sealed class TestEmbeddings : AiNexus.Features.Knowledge.Embeddings.IEmbeddingClient
{
    public string Provider => "ollama";
    public bool Enabled { get; set; } = true;
    public bool Fail { get; set; }
    public int? FailProfileId { get; set; }
    public int? FailOnCall { get; set; }
    public int LastProfileId { get; private set; }
    public int Calls;
    public int DelayMs { get; set; }
    public async Task<AiNexus.Features.Knowledge.Embeddings.EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, AiNexus.Features.Knowledge.Embeddings.EmbeddingPurpose purpose, AiNexus.Features.Knowledge.Embeddings.EmbeddingProfile profile, CancellationToken ct)
    {
        var call = Interlocked.Increment(ref Calls); LastProfileId = profile.Id; await Task.Delay(DelayMs, ct);
        if (Fail || FailProfileId == profile.Id || FailOnCall == call) throw new ExternalServiceException(Error.Unavailable("fixture_embedding_failed"), "測試索引服務暫停。");
        return new(inputs.Select(_ => { var value = new float[profile.Dimensions]; value[0] = 1; return value; }).ToArray());
    }
}
