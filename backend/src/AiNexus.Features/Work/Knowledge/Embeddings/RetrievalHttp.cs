using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Platform.Modules;
using System.Net;
using System.Text.Json;
using AiNexus.Features.Admin.Billing;
using AiNexus.Features.Platform.Inference.Runs;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

// 所有檢索模型端點共用同一套傳輸、有上限的重試與呼叫計量。
[Singleton]
public sealed partial class RetrievalHttp(IHttpClientFactory clients, ILogger<RetrievalHttp> logger)
{
    /// <summary>單一回應的上限；一批最多 128 段、每段向量最多數千維，文字化的浮點數遠小於此。</summary>
    private const int MaxResponseBytes = 32 * 1024 * 1024;

    /// <summary>期限由呼叫端（嵌入、重排各自的逾時）決定；嵌入與重排重送沒有副作用，所以暫時性失敗都重試。</summary>
    public async Task<JsonDocument> PostAsync(string client, string url, object payload, CancellationToken ct, string? apiKey = null)
    {
        using var response = await ExternalHttp.SendAsync(clients.CreateClient(client), () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
            if (apiKey is not null) request.Headers.Add("x-goog-api-key", apiKey);
            return request;
        }, retries: 2, ct, retry => LogRetrying(client, retry));
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException(response.StatusCode == HttpStatusCode.TooManyRequests ? KnowledgeErrors.RetrievalProviderRateLimited : KnowledgeErrors.RetrievalProviderUnavailable, "檢索模型服務目前無法使用，請確認端點、模型與配額。");
        return await BoundedHttpJson.ReadAsync(response, MaxResponseBytes, ct);
    }

    private void LogRetrying(string service, ExternalRetry retry)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["ExternalService"] = service, ["StatusCode"] = retry.Status, ["ErrorCode"] = "retrieval_retry" });
        LogRetry(logger, retry.Exception, retry.Attempt);
    }

    [LoggerMessage(EventId = 2002, EventName = "retrieval.retry", Level = LogLevel.Warning, Message = "Retrieval provider retry {Attempt} after transient failure.")]
    private static partial void LogRetry(ILogger logger, Exception? exception, int attempt);
}

[Singleton]
public sealed class RetrievalInvocation(IServiceScopeFactory scopes, IOptions<KnowledgeOptions> options, TimeProvider clock)
{
    public async Task<T> RunAsync<T>(Guid owner, string kind, string provider, string model, Func<Task<(T Value, long? Tokens)>> action, CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var billing = scope.ServiceProvider.GetRequiredService<BillingService>();
        var call = new ModelInvocation { OwnerId = owner, Kind = kind, Provider = provider, ModelId = model, CreatedAt = clock.GetUtcNow() };
        // 交易中最先取得的擁有者列鎖負責序列化配額預留；交易在呼叫供應商之前就結束，避免鎖在外部呼叫期間被持有。
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var start = call.CreatedAt.UtcDayStart(); var end = start.AddDays(1);
            // 所有嵌入與重排呼叫都經過這裡（位於索引與搜尋的深處），因此每日上限要回報成供應商限制（RateLimited），
            // 而不是一般的內部錯誤。
            if (await db.ModelInvocations.CountAsync(x => x.OwnerId == owner && (x.Kind == "embedding" || x.Kind == "rerank") && x.CreatedAt >= start && x.CreatedAt < end, ct) >= options.Value.Embedding.MaxDailyRequests)
                throw new ExternalServiceException(KnowledgeErrors.EmbeddingDailyQuota, "今日索引、語意查詢與重排次數已達系統上限，請稍後重試。");
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
