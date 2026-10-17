using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat.Runs;

/// <summary>
/// 生成事件保留期限的清理背景工作者。只刪 SSE 斷線重播用的中繼事件（RunEvents），不動對話與訊息。
/// 每小時分批刪除，每批各自提交。單一語句的行鎖數要遠低於 SQL Server 的鎖升級門檻（約 5,000），
/// 否則會升級成資料表鎖，阻塞正常的讀寫。
/// </summary>
public sealed partial class RunEventRetentionWorker(
    IServiceScopeFactory scopes,
    StorageReadiness storage,
    ILogger<RunEventRetentionWorker> logger,
    TimeProvider clock) : BackgroundService
{
    /// <summary>單次 DELETE 批次筆數上限（1,000 筆）。</summary>
    public const int BatchSize = 1000;

    /// <summary>事件保留時限（24 小時）。超過 24 小時且已結束的生成事件將被物理刪除。</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!storage.Configured) continue;

                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogCleanupFailed(logger, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>
    /// 分批刪除已過期且所屬生成已非活躍狀態的事件，直到無剩餘事件為止；回傳已刪除的總筆數。
    /// </summary>
    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var cutoff = clock.GetUtcNow() - Retention;

        var expired = db.RunEvents.Where(x =>
            x.CreatedAt < cutoff &&
            db.Runs.Any(r => r.Id == x.RunId && r.ActiveOwnerId == null));

        var total = 0;
        while (true)
        {
            var deleted = await expired.Take(BatchSize).ExecuteDeleteAsync(ct);
            total += deleted;
            if (deleted < BatchSize) return total;
        }
    }

    [LoggerMessage(EventId = 3004, EventName = "replay.cleanup_failed", Level = LogLevel.Warning, Message = "Replay event cleanup failed ({ErrorType}).")]
    private static partial void LogCleanupFailed(ILogger logger, string errorType);
}
