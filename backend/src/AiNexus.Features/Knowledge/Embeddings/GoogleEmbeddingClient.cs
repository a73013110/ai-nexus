using AiNexus.Platform.Errors;
using AiNexus.Features.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge.Embeddings;

public sealed class GoogleEmbeddingClient(RetrievalHttp http, IOptions<InferenceOptions> inference) : IEmbeddingClient
{
    public string Provider => "google";
    public async Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inference.Value.GoogleApiKey)) throw new ApiException(503, "google_api_key_missing", "尚未設定 Google AI API key。");
        var two = profile.Model.StartsWith("gemini-embedding-2", StringComparison.Ordinal);
        var requests = inputs.Select(text => {
            var config = new Dictionary<string, object?> { ["outputDimensionality"] = profile.Dimensions, ["autoTruncate"] = false };
            if (!two) config["taskType"] = purpose == EmbeddingPurpose.Document ? "RETRIEVAL_DOCUMENT" : "RETRIEVAL_QUERY";
            return new { model = "models/" + profile.Model, content = new { parts = new[] { new {
                text = two && purpose == EmbeddingPurpose.Query ? "task: search result | query: " + text : text
            } } }, embedContentConfig = config };
        }).ToArray();
        using var json = await http.PostAsync("GoogleAI", $"models/{Uri.EscapeDataString(profile.Model)}:batchEmbedContents", new { requests }, ct, inference.Value.GoogleApiKey);
        return new(json.RootElement.GetProperty("embeddings").EnumerateArray().Select(x => x.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray()).ToArray(),
            json.RootElement.TryGetProperty("usageMetadata", out var usage) && usage.TryGetProperty("promptTokenCount", out var count) ? count.GetInt64() : null);
    }
}
