using System.Net;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Time;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge.Embeddings;

// All retrieval model endpoints share transport, bounded retries and invocation accounting.
public sealed partial class RetrievalHttp(IHttpClientFactory clients, ILogger<RetrievalHttp> logger)
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
                    if (transient && attempt < 2) { await Delay(attempt, client, (int)response.StatusCode, null, ct); continue; }
                    throw new ExternalServiceException(response.StatusCode == HttpStatusCode.TooManyRequests ? Error.RateLimited("retrieval_provider_unavailable") : Error.Unavailable("retrieval_provider_unavailable"), "檢索模型服務目前無法使用，請確認端點、模型與配額。");
                }
                return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            }
            catch (HttpRequestException ex) when (attempt < 2) { await Delay(attempt, client, null, ex, ct); }
        }
    }
    private async Task Delay(int attempt, string service, int? status, Exception? exception, CancellationToken ct)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["ExternalService"] = service, ["StatusCode"] = status, ["ErrorCode"] = "retrieval_retry" });
        LogRetry(logger, exception, attempt + 1);
        await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt) + Random.Shared.Next(100)), ct);
    }

    [LoggerMessage(EventId = 2002, EventName = "retrieval.retry", Level = LogLevel.Warning, Message = "Retrieval provider retry {Attempt} after transient failure.")]
    private static partial void LogRetry(ILogger logger, Exception? exception, int attempt);
}

public sealed class RetrievalInvocation(IServiceScopeFactory scopes, IOptions<KnowledgeOptions> options, TimeProvider clock)
{
    public async Task<T> RunAsync<T>(Guid owner, string kind, string provider, string model, Func<Task<(T Value, long? Tokens)>> action, CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var billing = scope.ServiceProvider.GetRequiredService<AiNexus.Features.Billing.BillingService>();
        var call = new ModelInvocation { OwnerId = owner, Kind = kind, Provider = provider, ModelId = model, CreatedAt = clock.GetUtcNow() };
        // The owner row lock taken first in this transaction serializes the reservation; it ends before the provider call.
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var start = UtcDay.Start(call.CreatedAt); var end = start.AddDays(1);
            // Every embedding and rerank call passes through here, deep inside indexing and search, so the daily cap is
            // reported like a provider limit.
            if (await db.Set<ModelInvocation>().CountAsync(x => x.OwnerId == owner && (x.Kind == "embedding" || x.Kind == "rerank") && x.CreatedAt >= start && x.CreatedAt < end, ct) >= options.Value.MaxDailyEmbeddingRequests)
                throw new ExternalServiceException(Error.RateLimited("embedding_daily_quota"), "今日索引、語意查詢與重排次數已達系統上限，請稍後重試。");
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
            call.DurationMilliseconds = RunTiming.Milliseconds(call.CreatedAt, clock.GetUtcNow());
            await billing.MeterAsync(call.Id, call.InputTokens, call.OutputTokens, 0, 0, CancellationToken.None, call.Status == "completed");
            await billing.FinishAsync(call.Id, call.Status, CancellationToken.None); await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
