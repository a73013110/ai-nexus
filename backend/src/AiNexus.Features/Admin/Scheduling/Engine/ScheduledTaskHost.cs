using AiNexus.Data.App;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>
/// 排程迴圈：每 <see cref="SchedulingOptions.PollSeconds"/> 或被叫醒時認領到期任務，最多同時執行 <see cref="SchedulingOptions.MaxConcurrency"/> 個。
/// <see cref="SchedulingOptions.Execute"/> 關閉的程序不執行（管理頁照樣可用）。關閉時取消執行中的任務並等它們收尾。
/// </summary>
public sealed partial class ScheduledTaskHost(
    ScheduledTaskRunner runner,
    ScheduledTaskSignal signal,
    StorageReadiness readiness,
    IOptions<SchedulingOptions> options,
    ILogger<ScheduledTaskHost> logger,
    TimeProvider clock) : BackgroundService, IResidentService
{
    public static ResidentServiceDefinition Definition { get; } = new(
        "scheduling.engine", "排程引擎", "背景作業", "認領到期的排程任務並執行，管理逾時、停止與執行紀錄。", "依 Scheduling:PollSeconds 檢查到期任務；手動執行時立即喚醒")
    {
        Inactive = static services => services.GetRequiredService<IOptions<SchedulingOptions>>().Value.Execute ? null : "這個站台不執行排程（Scheduling:Execute），由其他站台執行。",
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Execute || !readiness.Configured) return;
        // 第一個 await 之前是同步執行，會擋住整個 host 的啟動。
        await Task.Yield();
        var running = new Dictionary<string, Task>(StringComparer.Ordinal);
        var initialized = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!initialized)
                {
                    await runner.InitializeAsync(stoppingToken);
                    initialized = true;
                }
                foreach (var key in running.Where(x => x.Value.IsCompleted).Select(x => x.Key).ToList()) running.Remove(key);
                foreach (var claim in await runner.ClaimDueAsync(settings.MaxConcurrency - running.Count, stoppingToken))
                {
                    // 各自在執行緒池跑：任務在第一個 await 前的同步部分不會卡住迴圈。
                    running[claim.Plan.Key] = Task.Run(() => runner.ExecuteAsync(claim, stoppingToken), CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogLoopFailed(logger, ex.GetType().Name);
            }

            try
            {
                await WaitAsync(running.Values, TimeSpan.FromSeconds(settings.PollSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
        // 執行中的任務收到同一個 stoppingToken，會記為 interrupted 並放掉租約。
        await Task.WhenAll(running.Values);
    }

    /// <summary>等到輪詢時間到、被叫醒，或有任務結束（空出的名額可以立刻補上）。</summary>
    private async Task WaitAsync(IEnumerable<Task> running, TimeSpan poll, CancellationToken ct)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            await Task.WhenAny([Task.Delay(poll, clock, wait.Token), signal.WaitAsync(wait.Token), .. running]);
        }
        finally
        {
            await wait.CancelAsync();
        }
        ct.ThrowIfCancellationRequested();
    }

    [LoggerMessage(EventId = 3805, EventName = "scheduling.loop_failed", Level = LogLevel.Warning, Message = "Scheduler poll failed ({ErrorType}); retrying on the next interval.")]
    private static partial void LogLoopFailed(ILogger logger, string errorType);
}
