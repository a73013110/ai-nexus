using AiNexus.Data.App;
using AiNexus.Features.Inference.Runs;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Chat.Runs;

/// <summary>
/// 每 15 秒為本機執行中的生成續約，並把資料庫端已被取消的生成同步到本機；最後交由
/// <see cref="RunLeaseRecovery"/> 復原超時的孤兒生成。續約若停止，其他程序會把這些生成誤判為超時。
/// </summary>
public sealed partial class RunRecoveryWorker(
    IServiceScopeFactory scopes,
    GenerationScheduler scheduler,
    StorageReadiness storage,
    ILogger<RunRecoveryWorker> logger,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!storage.Configured || !scheduler.Ready) continue;

                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var now = clock.GetUtcNow();
                    var tracked = scheduler.TrackedRuns;

                    if (tracked.Length > 0)
                    {
                        await db.Runs
                            .Where(x => tracked.Contains(x.Id) && x.ExecutorId == scheduler.InstanceId && x.ActiveOwnerId != null)
                            .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseExpiresAt, now + GenerationScheduler.LeaseDuration), stoppingToken);

                        // 本機仍在追蹤、但資料庫已不是活躍狀態的 Run，代表被別處取消或結算，這裡同步發出取消
                        var active = await db.Runs
                            .Where(x => tracked.Contains(x.Id) && x.ActiveOwnerId != null)
                            .Select(x => x.Id)
                            .ToArrayAsync(stoppingToken);

                        foreach (var id in tracked.Except(active))
                        {
                            scheduler.Cancel(id);
                        }
                    }

                    // 復原超時未續約的孤兒生成
                    await scope.ServiceProvider.GetRequiredService<RunLeaseRecovery>().RecoverAsync(now, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogPostponed(logger, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    [LoggerMessage(EventId = 3106, EventName = "generation.recovery_postponed", Level = LogLevel.Warning, Message = "Orphan recovery postponed ({ErrorType}).")]
    private static partial void LogPostponed(ILogger logger, string errorType);
}
