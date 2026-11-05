using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>
/// 未讀超過 <see cref="UnreadFor"/> 的通知改為已讀：放了一週還沒處理的事，徽章再亮也只是噪音；通知中心仍查得到。
/// 寫回資料而不是在查詢時換算，未讀的定義（<c>ReadAt</c> 為空）才只有一種，總覽的 procedure 不必跟著改。
/// 每小時分批更新，單一語句的行鎖數遠低於鎖升級門檻。
/// </summary>
public sealed partial class NotificationExpiryWorker(
    IServiceScopeFactory scopes,
    StorageReadiness storage,
    ILogger<NotificationExpiryWorker> logger,
    TimeProvider clock) : BackgroundService
{
    public const int BatchSize = 1000;

    public static readonly TimeSpan UnreadFor = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!storage.Configured) continue;
                try
                {
                    await ExpireAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogExpiryFailed(logger, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>把過期的未讀通知改為已讀，回傳筆數。</summary>
    public async Task<int> ExpireAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var cutoff = now - UnreadFor;
        var expired = db.Notifications.Unread().Where(x => x.CreatedAt < cutoff);
        var total = 0;
        while (true)
        {
            var updated = await expired.Take(BatchSize).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, now), ct);
            total += updated;
            if (updated < BatchSize) return total;
        }
    }

    [LoggerMessage(EventId = 3600, EventName = "notifications.expiry_failed", Level = LogLevel.Warning,
        Message = "Expiring unread notifications failed ({ErrorType}).")]
    private static partial void LogExpiryFailed(ILogger logger, string errorType);
}
