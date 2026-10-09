using System.Text.Json;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed class OpenAiCompatibleRerankClient(RetrievalHttp http, IOptions<KnowledgeOptions> options) : IRerankClient
{
    public string Provider => "openai-compatible";
    public async Task<IReadOnlyList<RerankScore>> RerankAsync(string query, IReadOnlyList<string> candidates, CancellationToken ct)
    {
        using var json = await http.PostAsync(KnowledgeModule.RetrievalModelsClient, options.Value.Rerank.Endpoint.TrimEnd('/') + "/v1/rerank", new { model = options.Value.Rerank.Model, query, documents = candidates, top_n = candidates.Count }, ct);
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("results", out var results)) throw RerankPayload.Invalid();
        return RerankPayload.Parse(results, "relevance_score");
    }
}

public static class RerankPayload
{
    public static IReadOnlyList<RerankScore> Parse(JsonElement root, string scoreName)
    {
        if (root.ValueKind != JsonValueKind.Array) throw Invalid();
        var scores = new List<RerankScore>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("index", out var index) || index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out var position)
                || !item.TryGetProperty(scoreName, out var score) || score.ValueKind != JsonValueKind.Number || !score.TryGetDouble(out var value) || !double.IsFinite(value)) throw Invalid();
            scores.Add(new(position, value));
        }
        return scores;
    }
    public static ApiException Invalid() => new(502, "rerank_invalid", "重排服務回應格式不正確。");
}
