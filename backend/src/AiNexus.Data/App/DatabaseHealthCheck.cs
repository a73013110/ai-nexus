using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiNexus.Data.App;

/// <summary>
/// 資料庫就緒性探針（Readiness Health Check）。
/// </summary>
/// <remarks>
/// 健康檢查端點是匿名的，且負載平衡器會頻繁探測，若每次都建立真實連線會拖累資料庫，因此結果會快取 <see cref="Reuse"/>。
/// 快取期間的並發請求共用同一個進行中的探測工作（由 <c>gate</c> 保護）。
/// 探測使用自己的逾時，而不是呼叫端的 Token：否則某個請求提前取消，會讓其他共用這次探測的請求也拿到失敗結果。
/// </remarks>
public sealed class DatabaseHealthCheck(
    IServiceScopeFactory scopes,
    StorageReadiness storage,
    TimeProvider clock) : IHealthCheck
{
    /// <summary>探測結果的快取期間，期間內的健康檢查共用同一次探測。</summary>
    public static readonly TimeSpan Reuse = TimeSpan.FromSeconds(5);

    /// <summary>單次探測的逾時上限。</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Lock gate = new();
    private Task<HealthCheckResult>? probe;
    private DateTimeOffset probedAt;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // 尚未設定資料庫連線時直接標記未就緒
        if (!storage.Configured)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("storage_not_configured"));
        }

        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (probe is null || (probe.IsCompleted && now - probedAt >= Reuse))
            {
                (probe, probedAt) = (ProbeAsync(), now);
            }

            return probe;
        }
    }

    private async Task<HealthCheckResult> ProbeAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(Timeout, clock);
            await using var scope = scopes.CreateAsyncScope();
            var connected = await scope.ServiceProvider
                .GetRequiredService<AppDbContext>()
                .Database.CanConnectAsync(timeout.Token);

            return connected
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("database_unavailable");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("database_unavailable");
        }
    }
}
