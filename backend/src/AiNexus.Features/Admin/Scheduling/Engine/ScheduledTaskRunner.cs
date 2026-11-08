using System.Collections.Concurrent;
using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>認領到的一輪：租約與這一輪的觸發資訊（手動要求在認領時從任務列移到這裡）。</summary>
public sealed record ScheduledTaskClaim(ScheduledTaskPlan Plan, Guid Lease, ScheduledTaskTrigger Trigger, Guid? RequestedBy, string? Parameters);

/// <summary>
/// 排程引擎的資料庫部分：補齊任務列、認領到期任務、執行一輪並收尾。迴圈在 <see cref="ScheduledTaskHost"/>；
/// 這裡的方法都是一次性的，測試直接呼叫。
/// </summary>
/// <remarks>
/// 跨程序互斥靠租約（與背景工作相同的 fencing）：認領是條件式 UPDATE，續租與收尾都比對 <c>LeaseToken</c>，
/// 程序中止後租約到期由其他程序接手。開發機與正式站共用資料庫也不會重疊執行。
/// </remarks>
[Singleton]
public sealed partial class ScheduledTaskRunner(
    IServiceScopeFactory scopes,
    ScheduledTaskCatalog catalog,
    ScheduledTaskSignal signal,
    Issues issues,
    ILogger<ScheduledTaskRunner> logger,
    TimeProvider clock)
{
    /// <summary>認領與每次續租把租約延長的時間；續租間隔（<see cref="Heartbeat"/>）必須遠小於它。</summary>
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);

    /// <summary>續租、讀停止要求與寫入進度的間隔；也是跨程序停止的最長反應時間。</summary>
    internal static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(5);

    internal static readonly string Host = Environment.MachineName.Length > ScheduledTask.HostMaxLength
        ? Environment.MachineName[..ScheduledTask.HostMaxLength] : Environment.MachineName;

    // 啟動任務是「每個程序啟動後跑一次」，做的事依程序本身（例如記錄這個程序的版本）。只有自己啟動時排上的才由自己認領：
    // IIS 重疊回收時舊程序還在輪詢，若讓它認領新程序排的那一輪，記下的會是舊版本，新版本就再也不會記。成功或沒事可做後移除。
    private readonly ConcurrentDictionary<string, byte> startupPending = new(StringComparer.Ordinal);

    /// <summary>
    /// 程序開始執行排程前呼叫一次：補上資料庫還沒有的任務列（第一次排程時間依各自的排程），並把啟動任務排到現在。
    /// 兩個程序同時補列時，後到的那個撞主鍵後重讀即可。
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var keys = catalog.All.Select(x => x.Key).ToList();
        var existing = await db.ScheduledTasks.Where(x => keys.Contains(x.Key)).Select(x => x.Key).ToListAsync(ct);
        foreach (var plan in catalog.All.Where(x => !existing.Contains(x.Key)))
        {
            db.Add(new ScheduledTask { Key = plan.Key, NextRunAt = plan.Schedule.First(now), UpdatedAt = now });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // 另一個程序剛補上同一列。
                db.ChangeTracker.Clear();
            }
        }

        var startup = catalog.All.Where(x => x.Schedule.RunsAtStartup && x.ExecutesHere).Select(x => x.Key).ToList();
        foreach (var key in startup) startupPending[key] = 0;
        if (startup.Count > 0)
            await db.ScheduledTasks.Where(x => startup.Contains(x.Key))
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.NextRunAt, now).SetProperty(x => x.UpdatedAt, now), ct);
    }

    /// <summary>
    /// 認領最多 <paramref name="limit"/> 個到期（未暫停且 <c>NextRunAt</c> 已到）或有手動要求的任務；不能執行或不歸這個程序執行（<c>ExecuteOnly</c>）的任務略過。
    /// 認領時清掉手動要求，所以一個要求只會執行一次；執行中才送達的要求留到這一輪結束後。
    /// </summary>
    public async Task<IReadOnlyList<ScheduledTaskClaim>> ClaimDueAsync(int limit, CancellationToken ct)
    {
        if (limit <= 0) return [];
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var runnable = catalog.All.Where(x => x.ExecutesHere && x.UnavailableReason(scope.ServiceProvider) is null).Select(x => x.Key).ToList();
        if (runnable.Count == 0) return [];
        var foreignStartup = catalog.All.Where(x => x.Schedule.RunsAtStartup && !startupPending.ContainsKey(x.Key)).Select(x => x.Key).ToList();

        var candidates = await db.ScheduledTasks.AsNoTracking()
            .Where(x => runnable.Contains(x.Key) && (x.LeaseUntil == null || x.LeaseUntil < now)
                && (x.RunRequestedAt != null || (!x.IsPaused && x.NextRunAt <= now && !foreignStartup.Contains(x.Key))))
            .OrderBy(x => x.RunRequestedAt ?? x.NextRunAt)
            .Take(limit)
            .ToListAsync(ct);

        var claims = new List<ScheduledTaskClaim>();
        foreach (var candidate in candidates)
        {
            var lease = Guid.NewGuid();
            // 條件包含讀到的手動要求：認領前剛送達的新要求不會被這次認領吃掉。
            var claimed = await db.ScheduledTasks
                .Where(x => x.Key == candidate.Key && x.RunRequestedAt == candidate.RunRequestedAt && (x.LeaseUntil == null || x.LeaseUntil < now))
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.LeaseToken, lease)
                    .SetProperty(x => x.LeaseUntil, now + LeaseDuration)
                    .SetProperty(x => x.LeaseHost, Host)
                    .SetProperty(x => x.CancelRequested, false)
                    .SetProperty(x => x.RunRequestedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.RunRequestedById, (Guid?)null)
                    .SetProperty(x => x.RunParameters, (string?)null)
                    .SetProperty(x => x.UpdatedAt, now), ct);
            if (claimed != 1) continue;
            var plan = catalog.Find(candidate.Key)!;
            var trigger = candidate.RunRequestedAt is not null ? ScheduledTaskTrigger.Manual
                : plan.Schedule.RunsAtStartup ? ScheduledTaskTrigger.Startup
                : ScheduledTaskTrigger.Scheduled;
            claims.Add(new(plan, lease, trigger, candidate.RunRequestedById, candidate.RunParameters));
        }
        return claims;
    }

    /// <summary>
    /// 執行認領到的一輪直到收尾，不拋例外。<paramref name="stop"/>（主機關閉）取消時記為 interrupted，
    /// 並把下次執行提前到現在，接手的程序會重跑這一輪。
    /// </summary>
    public async Task ExecuteAsync(ScheduledTaskClaim claim, CancellationToken stop)
    {
        try
        {
            await RunAsync(claim, stop);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stop.IsCancellationRequested)
        {
            // 連收尾都寫不進去（資料庫不可用）：租約到期後由下一次認領接手，留下的 running 紀錄會改成 interrupted。
            LogFinishFailed(logger, claim.Plan.Key, ex.GetType().Name);
        }
        catch (OperationCanceledException)
        {
            // 主機關閉時開始紀錄還沒寫完；租約到期後重跑。
        }
    }

    private async Task RunAsync(ScheduledTaskClaim claim, CancellationToken stop)
    {
        var plan = claim.Plan;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var activity = DiagnosticTrace.Start("scheduled_task.run");
        var started = clock.GetUtcNow();
        var trigger = Trigger(claim.Trigger);
        var run = new ScheduledTaskRun
        {
            TaskKey = plan.Key,
            TriggerKind = trigger,
            RequestedById = claim.Trigger == ScheduledTaskTrigger.Manual ? claim.RequestedBy : null,
            Host = Host,
            StartedAt = started,
            TraceId = activity.TraceId.ToHexString(),
        };
        // 已持有租約，其他還標著 running 的紀錄一定是中止的程序留下的。
        await db.ScheduledTaskRuns.Where(x => x.TaskKey == plan.Key && x.Status == ScheduledTaskRunStatuses.Running)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, ScheduledTaskRunStatuses.Interrupted)
                .SetProperty(x => x.FinishedAt, x => x.StartedAt > started ? x.StartedAt : started), stop);
        db.Add(run);
        await db.SaveChangesAsync(stop);
        db.ChangeTracker.Clear();

        activity.SetTag("operation.id", run.Id.ToString());
        using var logging = logger.BeginScope(new Dictionary<string, object?> { ["Kind"] = plan.Key, ["RunId"] = run.Id });
        LogStarted(logger, trigger);

        var context = new ScheduledTaskContext(claim.Trigger, claim.RequestedBy, claim.Parameters);
        using var cancel = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(plan.Timeout, clock);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(stop, cancel.Token, timeout.Token);
        using var monitor = new CancellationTokenSource();
        signal.Track(plan.Key, cancel);
        var heartbeat = HeartbeatAsync(plan.Key, claim.Lease, run.Id, context, cancel, monitor.Token);

        TaskOutcome outcome;
        string status;
        string? issue = null;
        try
        {
            outcome = await plan.Registration.Run(scope.ServiceProvider, context, execution.Token);
            status = outcome.Kind switch
            {
                TaskOutcomeKind.Succeeded => ScheduledTaskRunStatuses.Succeeded,
                TaskOutcomeKind.Idle => ScheduledTaskRunStatuses.Idle,
                _ => ScheduledTaskRunStatuses.Failed,
            };
            if (outcome.Error is { } error) issue = issues.Report(error);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            (status, outcome) = stop.IsCancellationRequested ? (ScheduledTaskRunStatuses.Interrupted, TaskOutcome.Idle())
                : timeout.IsCancellationRequested ? (ScheduledTaskRunStatuses.Failed, TaskOutcome.Failed(SchedulingErrors.TaskTimedOut))
                : (ScheduledTaskRunStatuses.Cancelled, TaskOutcome.Idle());
            if (outcome.Error is { } error) issue = issues.Report(error);
        }
        catch (Exception ex)
        {
            var error = (ex as ExternalServiceException)?.Error ?? SchedulingErrors.TaskFailed;
            (status, outcome) = (ScheduledTaskRunStatuses.Failed, TaskOutcome.Failed(error));
            issue = issues.Report(ex, error);
        }
        finally
        {
            signal.Untrack(plan.Key, cancel);
            await monitor.CancelAsync();
            await heartbeat;
        }

        // 收尾不受主機關閉影響：寫完才放掉租約，否則接手的程序看不到這一輪的結果。
        db.ChangeTracker.Clear();
        var finished = clock.GetUtcNow();
        var failed = status == ScheduledTaskRunStatuses.Failed;
        var progress = context.Progress;
        var message = outcome.Error is { } failure ? Issues.Message(failure, issue!) : null;
        await db.ScheduledTaskRuns.Where(x => x.Id == run.Id && x.Status == ScheduledTaskRunStatuses.Running)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.FinishedAt, finished < started ? started : finished)
                .SetProperty(x => x.Stage, Clip(progress?.Stage, ScheduledTaskRun.StageMaxLength))
                .SetProperty(x => x.CompletedUnits, progress?.Completed)
                .SetProperty(x => x.TotalUnits, progress?.Total)
                .SetProperty(x => x.Summary, Clip(outcome.Summary, ScheduledTaskRun.SummaryMaxLength))
                .SetProperty(x => x.ErrorCode, failed ? Clip(outcome.Error?.Code, ScheduledTaskRun.ErrorCodeMaxLength) : null)
                .SetProperty(x => x.IssueCode, failed ? issue : null)
                .SetProperty(x => x.ErrorMessage, failed ? Clip(message, ScheduledTaskRun.ErrorMessageMaxLength) : null), CancellationToken.None);

        var before = await db.ScheduledTasks.AsNoTracking().Where(x => x.Key == plan.Key && x.LeaseToken == claim.Lease)
            .Select(x => new { x.ConsecutiveFailures, x.NextRunAt }).SingleOrDefaultAsync(CancellationToken.None);
        if (before is null)
        {
            // 租約已被接手（停太久沒續到）：結果已記下，排程交給持有租約的程序。
            LogFinished(logger, status, (long)(finished - started).TotalMilliseconds);
            return;
        }

        // 中斷：下次執行提前到現在，讓接手的程序重跑；停止：照排程，不算失敗也不歸零。
        var previous = before.ConsecutiveFailures;
        var failures = failed ? previous + 1 : status is ScheduledTaskRunStatuses.Succeeded or ScheduledTaskRunStatuses.Idle ? 0 : previous;
        var next = status == ScheduledTaskRunStatuses.Interrupted
            ? before.NextRunAt is { } due && due <= finished ? due : finished
            : plan.Schedule.Next(finished, failed);
        await db.ScheduledTasks.Where(x => x.Key == plan.Key && x.LeaseToken == claim.Lease)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseHost, (string?)null)
                .SetProperty(x => x.CancelRequested, false)
                .SetProperty(x => x.NextRunAt, next)
                .SetProperty(x => x.ConsecutiveFailures, failures)
                .SetProperty(x => x.UpdatedAt, finished), CancellationToken.None);
        if (status is ScheduledTaskRunStatuses.Succeeded or ScheduledTaskRunStatuses.Idle) startupPending.TryRemove(plan.Key, out _);
        LogFinished(logger, status, (long)(finished - started).TotalMilliseconds);

        try
        {
            var alerts = scope.ServiceProvider.GetRequiredService<FailureAlerts>();
            if (failed && failures == plan.Definition.NotifyAfterFailures) await alerts.RaiseAsync(plan, run.Id, failures, issue, CancellationToken.None);
            else if (failures == 0 && previous >= plan.Definition.NotifyAfterFailures) await alerts.ResolveAsync(plan.Key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogAlertFailed(logger, plan.Key, ex.GetType().Name);
        }
    }

    /// <summary>
    /// 每 <see cref="Heartbeat"/> 續租一次並寫入進度；租約被接手或收到停止要求（續租的條件不成立）時取消這一輪。
    /// 續租本身失敗（資料庫暫時不可用）不取消：租約還有效，下一次續租再試，真的過期了下一次就會發現。
    /// </summary>
    private async Task HeartbeatAsync(string key, Guid lease, Guid runId, ScheduledTaskContext context, CancellationTokenSource cancel, CancellationToken ct)
    {
        ScheduledTaskProgress? written = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Heartbeat, clock, ct);
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var now = clock.GetUtcNow();
                    var renewed = await db.ScheduledTasks
                        .Where(x => x.Key == key && x.LeaseToken == lease && !x.CancelRequested)
                        .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseUntil, now + LeaseDuration), ct);
                    if (renewed == 0)
                    {
                        await cancel.CancelAsync();
                        return;
                    }
                    var progress = context.Progress;
                    if (progress is not null && progress != written)
                    {
                        await db.ScheduledTaskRuns.Where(x => x.Id == runId && x.Status == ScheduledTaskRunStatuses.Running)
                            .ExecuteUpdateAsync(p => p
                                .SetProperty(x => x.Stage, Clip(progress.Stage, ScheduledTaskRun.StageMaxLength))
                                .SetProperty(x => x.CompletedUnits, progress.Completed)
                                .SetProperty(x => x.TotalUnits, progress.Total), ct);
                        written = progress;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogHeartbeatFailed(logger, key, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static string Trigger(ScheduledTaskTrigger trigger) => trigger switch
    {
        ScheduledTaskTrigger.Manual => ScheduledTaskTriggers.Manual,
        ScheduledTaskTrigger.Startup => ScheduledTaskTriggers.Startup,
        _ => ScheduledTaskTriggers.Scheduled,
    };

    private static string? Clip(string? value, int max) => value is { Length: > 0 } text && text.Length > max ? text[..max] : value;

    [LoggerMessage(EventId = 3800, EventName = "scheduling.run_started", Level = LogLevel.Information, Message = "Scheduled task run started ({Reason}).")]
    private static partial void LogStarted(ILogger logger, string reason);

    [LoggerMessage(EventId = 3801, EventName = "scheduling.run_finished", Level = LogLevel.Information, Message = "Scheduled task run finished with {Stage} in {DurationMs} ms.")]
    private static partial void LogFinished(ILogger logger, string stage, long durationMs);

    [LoggerMessage(EventId = 3802, EventName = "scheduling.heartbeat_failed", Level = LogLevel.Warning, Message = "Scheduled task {Kind} lease renewal failed ({ErrorType}); retrying.")]
    private static partial void LogHeartbeatFailed(ILogger logger, string kind, string errorType);

    [LoggerMessage(EventId = 3803, EventName = "scheduling.finish_failed", Level = LogLevel.Warning, Message = "Scheduled task {Kind} run could not be recorded ({ErrorType}); it will be retried after the lease expires.")]
    private static partial void LogFinishFailed(ILogger logger, string kind, string errorType);

    [LoggerMessage(EventId = 3804, EventName = "scheduling.alert_failed", Level = LogLevel.Warning, Message = "Scheduled task {Kind} failure notification could not be written ({ErrorType}).")]
    private static partial void LogAlertFailed(ILogger logger, string kind, string errorType);
}
