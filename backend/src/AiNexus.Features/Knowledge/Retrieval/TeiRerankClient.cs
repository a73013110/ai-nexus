using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class TeiRerankClient(RetrievalHttp http, IOptions<KnowledgeOptions> options) : IRerankClient
{
    public string Provider => "tei";
    public async Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct)
    {
        using var json = await http.PostAsync(KnowledgeModule.RetrievalModelsClient, options.Value.Rerank.Endpoint.TrimEnd('/') + "/rerank", new { query, texts = candidates, truncate = false, raw_scores = false, return_text = false }, ct);
        return RerankPayload.Parse(json.RootElement, "score");
    }
}
