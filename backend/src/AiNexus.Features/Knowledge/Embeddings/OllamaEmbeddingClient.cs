using AiNexus.Features.Inference;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class OllamaEmbeddingClient(RetrievalHttp http, IOptions<KnowledgeOptions> options, IOptions<InferenceOptions> inference) : IEmbeddingClient
{
    public string Provider => "ollama";
    public async Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct)
    {
        var endpoint = options.Value.Endpoint.Length > 0 ? options.Value.Endpoint : inference.Value.BaseUrl;
        using var json = await http.PostAsync(KnowledgeModule.RetrievalModelsClient, endpoint.TrimEnd('/') + "/api/embed", new {
            model = profile.Model, input = inputs.Select(x => EmbeddingInput.Format(profile, x, purpose)).ToArray(), dimensions = profile.Dimensions, truncate = false
        }, ct);
        return new(json.RootElement.GetProperty("embeddings").EnumerateArray().Select(x => x.EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray(),
            json.RootElement.TryGetProperty("prompt_eval_count", out var count) ? count.GetInt64() : null);
    }
}
