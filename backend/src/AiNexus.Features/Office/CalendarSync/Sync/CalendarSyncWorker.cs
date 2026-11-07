using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>
/// 每 <see cref="CalendarSyncOptions.IntervalSeconds"/> 跑一輪 <see cref="CalendarSyncRunner"/>；管理頁的「立即同步」經 <see cref="CalendarSyncState.Request"/> 叫醒它提前執行。
/// 未啟用時不執行（手動同步的端點也會拒絕）。每天清一次超過保存期的執行紀錄。
/// </summary>
public sealed partial class CalendarSyncWorker(
    IServiceScopeFactory scopes,
    CalendarSyncState state,
    IOptions<CalendarSyncOptions> options,
    ILogger<CalendarSyncWorker> logger,
    TimeProvider clock) : BackgroundService
{
    private static readonly TimeSpan RetentionInterval = TimeSpan.FromDays(1);
    private DateTimeOffset nextRetention;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        // 第一個 await 之前是同步執行，會擋住整個 host 的啟動；GD 或 Google 慢的時候不能拖住網站。
        await Task.Yield();
        CalendarSyncRequest? request = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CalendarSyncRunner>().RunAsync(request, stoppingToken);
                await CleanUpAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogRunAborted(logger, ex.GetType().Name);
            }
            request = await NextAsync(stoppingToken);
        }
    }

    /// <summary>等到下一輪的時間或手動同步的請求，先到者為準；回傳手動請求，排程輪為 null。</summary>
    private async Task<CalendarSyncRequest?> NextAsync(CancellationToken ct)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var delay = Task.Delay(TimeSpan.FromSeconds(options.Value.IntervalSeconds), clock, wait.Token);
        var requested = state.Requests.WaitToReadAsync(wait.Token).AsTask();
        try
        {
            await Task.WhenAny(delay, requested);
        }
        finally
        {
            await wait.CancelAsync();
        }
        ct.ThrowIfCancellationRequested();
        return state.Requests.TryRead(out var request) ? request : null;
    }

    /// <summary>執行紀錄依開始時間分批刪除（走 <c>ix_SyncRuns_StartedAt</c>），每批自動提交，不開長交易。</summary>
    private async Task CleanUpAsync(AppDbContext db, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (now < nextRetention) return;
        nextRetention = now + RetentionInterval;
        var cutoff = now.AddDays(-options.Value.RunRetentionDays);
        try
        {
            while (await db.SyncRuns.Where(x => x.StartedAt < cutoff).OrderBy(x => x.StartedAt).Take(1000).ExecuteDeleteAsync(ct) == 1000) { }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRetentionFailed(logger, ex.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 3705, EventName = "calendar_sync.run_aborted", Level = LogLevel.Warning, Message = "Calendar sync run aborted ({ErrorType}); it will be retried on the next interval.")]
    private static partial void LogRunAborted(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3706, EventName = "calendar_sync.retention_failed", Level = LogLevel.Warning, Message = "Calendar sync run history cleanup failed ({ErrorType}); it will be retried tomorrow.")]
    private static partial void LogRetentionFailed(ILogger logger, string errorType);
}
