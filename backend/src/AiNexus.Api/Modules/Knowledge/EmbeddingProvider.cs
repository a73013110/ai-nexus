using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Knowledge;

public sealed class KnowledgeOptions
{
    public string EmbeddingProvider { get; set; } = "google";
    public string EmbeddingModel { get; set; } = "gemini-embedding-2";
    public int Dimensions { get; set; } = 768;
    public string InputFormat { get; set; } = "plain";
    public string QueryInstruction { get; set; } = "Given a web search query, retrieve relevant passages that answer the query";
    public string Revision { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public bool UseNativeVector { get; set; } = true;
    public int MaxDailyEmbeddingRequests { get; set; } = 2000;
    public int PortableCandidateLimit { get; set; } = 2000;
    public int MaxCollections { get; set; } = 30;
    public int MaxDocumentsPerCollection { get; set; } = 100;
    public int ChunkCharacters { get; set; } = 600;
    public int ChunkOverlap { get; set; } = 80;
    public int TopK { get; set; } = 6;
    public int ContextCharacters { get; set; } = 5000;
}
public interface IEmbeddingProvider
{
    string Profile { get; }
    bool Enabled { get; }
    Task<float[]> EmbedAsync(Guid owner, string text, bool document, string? title, CancellationToken ct);
}
public sealed class EmbeddingProvider(IHttpClientFactory clients, IOptions<KnowledgeOptions> knowledge, IOptions<InferenceOptions> inference, IServiceScopeFactory scopes, ModelQuotaLock writes) : IEmbeddingProvider
{
    public string Profile => EmbeddingInput.Profile(knowledge.Value);
    public bool Enabled => knowledge.Value.EmbeddingProvider != "none";
    public async Task<float[]> EmbedAsync(Guid owner, string text, bool document, string? title, CancellationToken ct)
    {
        if (!Enabled) throw new ApiException(409, "embeddings_disabled", "目前採用關鍵字檢索，未啟用語意向量。");
        if (knowledge.Value.EmbeddingProvider == "google" && string.IsNullOrWhiteSpace(inference.Value.GoogleApiKey)) throw new ApiException(503, "google_api_key_missing", "尚未設定 Google AI API key。");
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var billing = scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Billing.BillingService>();
        var call = new ModelInvocation { OwnerId = owner, Kind = "embedding", ModelId = knowledge.Value.EmbeddingModel };
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var since = UtcDay.Start(call.CreatedAt); var until = since.AddDays(1);
            if (await db.Set<ModelInvocation>().CountAsync(x => x.OwnerId == owner && x.Kind == "embedding" && x.CreatedAt >= since && x.CreatedAt < until, ct) >= knowledge.Value.MaxDailyEmbeddingRequests)
                throw new ApiException(429, "embedding_daily_quota", "今日索引與語意查詢次數已達系統上限，請稍後重試。");
            await billing.ReserveAsync(call.Id, owner, null, knowledge.Value.EmbeddingProvider, call.ModelId, "embedding", call.CreatedAt, ct);
            db.Add(call); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        finally { writes.Gate.Release(); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(knowledge.Value.TimeoutSeconds));
        try
        {
            await billing.StartAsync(call.Id, ct); await db.SaveChangesAsync(ct);
            float[] vector;
            if (knowledge.Value.EmbeddingProvider == "google")
            {
                var two = knowledge.Value.EmbeddingModel.StartsWith("gemini-embedding-2", StringComparison.Ordinal);
                var payload = new Dictionary<string, object?>
                {
                    ["model"] = "models/" + knowledge.Value.EmbeddingModel,
                    ["content"] = new { parts = new[] { new { text = two ? document ? $"title: {title ?? "none"} | text: {text}" : $"task: search result | query: {text}" : text } } },
                    ["embedContentConfig"] = new Dictionary<string, object?> { ["outputDimensionality"] = knowledge.Value.Dimensions, ["autoTruncate"] = false }
                };
                if (!two) ((Dictionary<string, object?>)payload["embedContentConfig"]!)["taskType"] = document ? "RETRIEVAL_DOCUMENT" : "RETRIEVAL_QUERY";
                using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{Uri.EscapeDataString(knowledge.Value.EmbeddingModel)}:embedContent") { Content = JsonContent.Create(payload) };
                request.Headers.Add("x-goog-api-key", inference.Value.GoogleApiKey);
                using var response = await clients.CreateClient("GoogleAI").SendAsync(request, timeout.Token);
                RequireSuccess(response);
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
                vector = json.RootElement.GetProperty("embedding").GetProperty("values").EnumerateArray().Select(x => x.GetSingle()).ToArray();
                if (json.RootElement.TryGetProperty("usageMetadata", out var usage) && usage.TryGetProperty("promptTokenCount", out var count)) call.InputTokens = count.GetInt64();
            }
            else
            {
                using var response = await clients.CreateClient("Ollama").PostAsJsonAsync("api/embed", new { model = knowledge.Value.EmbeddingModel, input = EmbeddingInput.Format(knowledge.Value, text, document), dimensions = knowledge.Value.Dimensions, truncate = false }, timeout.Token);
                RequireSuccess(response); using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
                vector = json.RootElement.GetProperty("embeddings")[0].EnumerateArray().Select(x => x.GetSingle()).ToArray();
                if (json.RootElement.TryGetProperty("prompt_eval_count", out var count)) call.InputTokens = count.GetInt64();
            }
            if (vector.Length != knowledge.Value.Dimensions || vector.Any(x => !float.IsFinite(x))) throw new ApiException(502, "embedding_invalid", "向量回應維度或數值不正確。");
            var norm = Math.Sqrt(vector.Sum(x => (double)x * x));
            if (norm < 1e-12) throw new ApiException(502, "embedding_invalid", "向量回應無法正規化。");
            for (var i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
            call.Status = "completed"; call.OutputTokens = 0; return vector;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { call.Status = "cancelled"; throw; }
        catch (OperationCanceledException) { call.Status = "failed"; throw new ApiException(504, "embedding_timeout", "語意索引服務逾時，請重試。"); }
        catch { call.Status = "failed"; throw; }
        finally { await billing.MeterAsync(call.Id, call.InputTokens, call.OutputTokens, 0, 0, CancellationToken.None, call.Status == "completed"); await billing.FinishAsync(call.Id, call.Status, CancellationToken.None); await db.SaveChangesAsync(CancellationToken.None); }
    }
    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw response.StatusCode == HttpStatusCode.TooManyRequests
            ? new ApiException(429, "embedding_provider_quota", "向量模型額度或速率受限，請稍後重試。")
            : new ApiException(503, "embedding_provider_unavailable", "向量模型目前無法使用，請確認模型名稱、API 權限與服務設定。");
    }
}
