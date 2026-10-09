using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Time;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge;

public enum EmbeddingPurpose { Document, Query }
public sealed record EmbeddingBatchResult(IReadOnlyList<float[]> Vectors, long? InputTokens = null);
public interface IEmbeddingClient
{
    string Provider { get; }
    Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct);
}
// All retrieval model endpoints share transport, bounded retries and invocation accounting.
public sealed class RetrievalHttp(IHttpClientFactory clients, ILogger<RetrievalHttp> logger)
{
    public async Task<JsonDocument> PostAsync(string client, string url, object payload, CancellationToken ct, string? apiKey = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
                if (apiKey is not null) request.Headers.Add("x-goog-api-key", apiKey);
                using var response = await clients.CreateClient(client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                    if (transient && attempt < 2) { await Delay(attempt, ct, client, (int)response.StatusCode); continue; }
                    throw new ApiException(response.StatusCode == HttpStatusCode.TooManyRequests ? 429 : 503, "retrieval_provider_unavailable", "檢索模型服務目前無法使用，請確認端點、模型與配額。");
                }
                return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            }
            catch (HttpRequestException ex) when (attempt < 2) { await Delay(attempt, ct, client, exception: ex); }
        }
    }
    private async Task Delay(int attempt, CancellationToken ct, string service, int? status = null, Exception? exception = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["ExternalService"] = service, ["StatusCode"] = status, ["ErrorCode"] = "retrieval_retry" });
        logger.LogWarning(new EventId(2002, "retrieval.retry"), exception, "Retrieval provider retry {Attempt} after transient failure.", attempt + 1);
        await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt) + Random.Shared.Next(100)), ct);
    }
}
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
public sealed class NoneEmbeddingClient : IEmbeddingClient
{
    public string Provider => "none";
    public Task<EmbeddingBatchResult> EmbedBatchAsync(IReadOnlyList<string> inputs, EmbeddingPurpose purpose, EmbeddingProfile profile, CancellationToken ct)
        => throw new ApiException(409, "embeddings_disabled", "目前採用全文檢索，未啟用語意向量。");
}
public sealed class RetrievalInvocation(IServiceScopeFactory scopes, IOptions<KnowledgeOptions> options)
{
    public async Task<T> RunAsync<T>(Guid owner, string kind, string provider, string model, Func<Task<(T Value, long? Tokens)>> action, CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var billing = scope.ServiceProvider.GetRequiredService<AiNexus.Features.Billing.BillingService>();
        var call = new ModelInvocation { OwnerId = owner, Kind = kind, Provider = provider, ModelId = model };
        // The owner row lock taken first in this transaction serializes the reservation; it ends before the provider call.
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var start = UtcDay.Start(call.CreatedAt); var end = start.AddDays(1);
            if (await db.Set<ModelInvocation>().CountAsync(x => x.OwnerId == owner && (x.Kind == "embedding" || x.Kind == "rerank") && x.CreatedAt >= start && x.CreatedAt < end, ct) >= options.Value.MaxDailyEmbeddingRequests)
                throw new ApiException(429, "embedding_daily_quota", "今日索引、語意查詢與重排次數已達系統上限，請稍後重試。");
            await billing.ReserveAsync(call.Id, owner, null, provider, model, kind, call.CreatedAt, ct);
            db.Add(call); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        try
        {
            await billing.StartAsync(call.Id, ct); await db.SaveChangesAsync(ct);
            var result = await action(); call.InputTokens = result.Tokens; call.OutputTokens = 0; call.Status = "completed"; return result.Value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { call.Status = "cancelled"; throw; }
        catch { call.Status = "failed"; throw; }
        finally
        {
            call.DurationMilliseconds = RunTiming.Milliseconds(call.CreatedAt, DateTimeOffset.UtcNow);
            await billing.MeterAsync(call.Id, call.InputTokens, call.OutputTokens, 0, 0, CancellationToken.None, call.Status == "completed");
            await billing.FinishAsync(call.Id, call.Status, CancellationToken.None); await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
public sealed class EmbeddingBatchScheduler(IOptions<KnowledgeOptions> options, GenerationScheduler generation)
{
    private readonly SemaphoreSlim gate = new(options.Value.MaxConcurrentBatches);
    public async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        while (true)
        {
            while (generation.Generating || generation.QueueDepth > 0) await Task.Delay(200, ct);
            await gate.WaitAsync(ct);
            if (!generation.Generating && generation.QueueDepth == 0) return new Lease(gate);
            gate.Release();
        }
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
}
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
