using System.Diagnostics;
using System.Linq.Expressions;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Scheduling;
using AiNexus.Platform.Threading;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 依 <see cref="JobLane"/> 消費持久佇列：同通道依建立時間先後，不同通道互不等待。
/// 同一份資料由不同通道的工作同時處理時，靠資料庫鎖與 fencing 檢查點保證正確（本機與 IIS 共用資料庫時本來就會同時執行）。
/// </summary>
public sealed partial class BackgroundJobWorker(
    IServiceScopeFactory scopes,
    StorageReadiness readiness,
    JobQueueSignal signal,
    IEnumerable<BulkJobKind> bulkKinds,
    ILogger<BackgroundJobWorker> logger,
    Issues issues,
    TimeProvider clock) : BackgroundService, IResidentService
{
    public static ResidentServiceDefinition Definition { get; } = new(
        "jobs.worker", "佇列工作處理", "背景工作", "執行使用者送出的文件辨識、評測、程式碼 Review 等工作。", "有工作就立即處理；每 5 秒檢查其他站台排入的工作");

    // 同程序排入的工作靠 JobQueueSignal 立即開始；輪詢只負責其他程序排入的工作與到期的租約。
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UnavailableDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);

    private static readonly (JobLane Lane, int Consumers)[] Lanes = [(JobLane.Standard, 2), (JobLane.Bulk, 1)];

    private readonly string[] bulk = [.. bulkKinds.Select(x => x.Kind)];

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        Lanes.SelectMany(x => Enumerable.Range(0, x.Consumers).Select(_ => ConsumeAsync(x.Lane, stoppingToken))));

    private async Task ConsumeAsync(JobLane lane, CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            var wake = signal.Next;
            try
            {
                if (readiness.Configured && await ProcessAsync(lane, stoppingToken)) continue;
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                await Task.WhenAny(wake, Task.Delay(PollInterval, clock, wait.Token));
                await wait.CancelAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogQueueUnavailable(logger, ex.GetType().Name);
                await Task.Delay(UnavailableDelay, clock, stoppingToken);
            }
        }
    }

    /// <summary>認領並執行下一件工作，不分通道。回傳 false 表示沒有可認領的工作。測試直接呼叫。</summary>
    public Task<bool> ProcessNextAsync(CancellationToken stop) => ProcessAsync(null, stop);

    /// <summary>只認領 <paramref name="lane"/> 的工作。測試直接呼叫。</summary>
    public Task<bool> ProcessNextAsync(JobLane lane, CancellationToken stop) => ProcessAsync(lane, stop);

    private IQueryable<BackgroundJob> InLane(IQueryable<BackgroundJob> jobs, JobLane? lane) => lane switch
    {
        JobLane.Bulk => jobs.Where(x => bulk.Contains(x.Kind)),
        JobLane.Standard => jobs.Where(x => !bulk.Contains(x.Kind)),
        _ => jobs,
    };

    private async Task<bool> ProcessAsync(JobLane? lane, CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var candidate = await InLane(db.BackgroundJobs.AsNoTracking(), lane)
            .Where(Claimable(now))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Status })
            .FirstOrDefaultAsync(stop);
        if (candidate is null) return false;
        var id = candidate.Id;
        // 執行中卻租約到期，表示前一個 worker 中止了，這次是自動接手；中止的原因可能正是這個工作。
        var recovered = candidate.Status == RunStates.Running;

        // 以條件式 UPDATE 認領工作：只有仍是讀到的可認領狀態的列會被改寫，其他 worker 搶到就直接放棄這次嘗試。
        var lease = Guid.NewGuid();
        var claimed = await db.BackgroundJobs
            .Where(Claimable(now))
            .Where(x => x.Id == id && x.Status == candidate.Status)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, RunStates.Running)
                .SetProperty(x => x.LeaseToken, lease)
                .SetProperty(x => x.LeaseUntil, now.AddSeconds(JobService.LeaseSeconds))
                .SetProperty(x => x.Attempt, x => x.Attempt + 1)
                .SetProperty(x => x.UpdatedAt, now), stop);
        if (claimed != 1) return true;

        var job = await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == id, stop);
        if (job.TraceId is null || job.ParentSpanId is null || job.OperationId == Guid.Empty)
        {
            job.TraceId ??= ActivityTraceId.CreateRandom().ToHexString();
            job.ParentSpanId ??= ActivitySpanId.CreateRandom().ToHexString();
            job.OperationId = job.Id;
            await db.BackgroundJobs
                .Where(x => x.Id == id && x.LeaseToken == lease)
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.TraceId, job.TraceId)
                    .SetProperty(x => x.ParentSpanId, job.ParentSpanId)
                    .SetProperty(x => x.OperationId, job.OperationId), stop);
        }

        using var activity = DiagnosticTrace.Start("job.execute", job.TraceId, job.ParentSpanId, ActivityKind.Consumer);
        activity.SetTag("operation.id", job.OperationId.ToString());
        using var logging = logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = id,
            ["OperationId"] = job.OperationId,
            ["UserId"] = job.OwnerId,
            ["Attempt"] = job.Attempt,
            ["Kind"] = job.Kind,
            ["RequestId"] = null,
        });
        LogStarted(logger, job.Attempt);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(stop);
        using var monitor = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var heartbeat = MonitorAsync(id, lease, execution, monitor.Token);

        var status = RunStates.Completed;
        string? code = null, message = null, issue = null;
        try
        {
            if (job.CancelRequested)
            {
                execution.Cancel();
                execution.Token.ThrowIfCancellationRequested();
            }
            // 種類沒有（或已不再有）處理器時為 null。
            var handler = scope.ServiceProvider.GetRequiredService<KeyedServices<IBackgroundJobHandler>>().Find(job.Kind);
            var outcome = recovered && job.Attempt > JobService.MaxRecoveredAttempts ? JobsErrors.JobRecoveryLimit
                : handler is null ? JobsErrors.JobHandlerMissing
                : await handler.ExecuteAsync(new(db, job, lease), execution.Token);
            if (!outcome.IsSuccess)
            {
                status = RunStates.Failed;
                code = outcome.Error.Code;
                issue = issues.Report(outcome.Error);
                message = Issues.Message(outcome.Error, issue);
            }
        }
        catch (OperationCanceledException)
        {
            status = RunStates.Cancelled;
        }
        catch (Exception ex)
        {
            status = RunStates.Failed;
            var error = (ex as ExternalServiceException)?.Error ?? JobsErrors.JobProcessingFailed;
            code = error.Code;
            issue = issues.Report(ex, error);
            message = Issues.Message(error, issue);
        }
        finally
        {
            monitor.Cancel();
            try
            {
                await heartbeat;
            }
            catch (OperationCanceledException)
            {
            }
        }

        // 主機關閉時保留租約與檢查點，讓其他程序之後能接續執行，因此這裡不寫入終態。
        if (stop.IsCancellationRequested) return true;

        // 處理器的變更只能經由帶正確 fencing 租約的檢查點保存。寫終態前先清掉未完成的追蹤變更，
        // 終態與通知則另開一個交易，避免處理器殘留的變更一起被寫入。
        db.ChangeTracker.Clear();
        var stage = status switch
        {
            RunStates.Completed => "處理完成",
            RunStates.Cancelled => "已取消",
            _ => "需要重試",
        };
        await using var completion = await db.Database.BeginTransactionAsync(CancellationToken.None);
        var finished = await db.BackgroundJobs
            .Where(x => x.Id == id && x.LeaseToken == lease && x.Status == RunStates.Running)
            .ExecuteUpdateAsync(p => p
                // 取消要求以資料庫當下的值為準：執行期間才送達的取消也會收尾為 cancelled。
                .SetProperty(x => x.Status, x => x.CancelRequested ? RunStates.Cancelled : status)
                .SetProperty(x => x.Stage, x => x.CancelRequested ? "已取消" : stage)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.ActiveKey, (string?)null)
                .SetProperty(x => x.ErrorCode, code)
                .SetProperty(x => x.IssueCode, issue)
                .SetProperty(x => x.ErrorMessage, message)
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), CancellationToken.None);
        if (finished == 1)
        {
            var final = await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken.None);
            var isRepositoryReview = final.Kind == "repository-review";
            var (level, title) = final.Status switch
            {
                RunStates.Completed => ("success", "背景任務已完成"),
                RunStates.Failed => ("error", "背景任務需要重試"),
                _ => ("info", "背景任務已取消"),
            };
            await scope.ServiceProvider.GetRequiredService<NotificationService>().PublishAsync(
                new NotificationMessage(final.OwnerId, $"job:{id}:{final.Attempt}", "task." + final.Status, level, title,
                    final.Status == RunStates.Failed ? final.ErrorMessage ?? Issues.Message(final.IssueCode) : final.Label,
                    isRepositoryReview ? "repository-review" : "task",
                    isRepositoryReview ? final.SubjectId : final.Id)
                { IssueCode = final.IssueCode, OwnAction = true },
                CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        await completion.CommitAsync(CancellationToken.None);
        LogFinished(logger, status);
        return true;
    }

    private Task MonitorAsync(Guid id, Guid lease, CancellationTokenSource execution, CancellationToken ct) => LeaseHeartbeat.RunAsync(
        MonitorInterval,
        clock,
        async token =>
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.BackgroundJobs
                .Where(x => x.Id == id && x.LeaseToken == lease && x.Status == RunStates.Running && !x.CancelRequested)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseUntil, clock.GetUtcNow().AddSeconds(JobService.LeaseSeconds)), token) == 1;
        },
        ex => LogLeaseRenewalFailed(logger, id, ex.GetType().Name),
        execution,
        ct);

    // 可認領：排隊中，或執行中但租約已過期（原 worker 停止續租）。挑選與條件式 UPDATE 必須用同一條件。
    private static Expression<Func<BackgroundJob, bool>> Claimable(DateTimeOffset now) =>
        x => x.Status == RunStates.Queued || (x.Status == RunStates.Running && x.LeaseUntil < now);

    [LoggerMessage(EventId = 3000, EventName = "job.started", Level = LogLevel.Information, Message = "Background job started; attempt {Attempt}.")]
    private static partial void LogStarted(ILogger logger, int attempt);

    [LoggerMessage(EventId = 3001, EventName = "job.finished", Level = LogLevel.Information, Message = "Background job finished with {Stage}.")]
    private static partial void LogFinished(ILogger logger, string stage);

    [LoggerMessage(EventId = 3002, EventName = "job.queue_unavailable", Level = LogLevel.Warning, Message = "Background queue unavailable ({ErrorType}).")]
    private static partial void LogQueueUnavailable(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3003, EventName = "job.lease_renewal_failed", Level = LogLevel.Warning, Message = "Job {JobId} lease renewal failed ({ErrorType}); retrying.")]
    private static partial void LogLeaseRenewalFailed(ILogger logger, Guid jobId, string errorType);
}
