using System.Diagnostics;
using System.Linq.Expressions;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

public sealed partial class BackgroundJobWorker(
    IServiceScopeFactory scopes,
    StorageReadiness readiness,
    ILogger<BackgroundJobWorker> logger,
    Issues issues,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!readiness.Configured || !await ProcessNextAsync(stoppingToken)) await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogQueueUnavailable(logger, ex.GetType().Name);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var candidate = await db.Set<BackgroundJob>().AsNoTracking()
            .Where(Claimable(now))
            .OrderBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(stop);
        if (candidate is not Guid id) return false;

        // 以條件式 UPDATE 認領工作：只有仍是可認領狀態的列會被改寫，其他 worker 搶到就直接放棄這次嘗試。
        var lease = Guid.NewGuid();
        var claimed = await db.Set<BackgroundJob>()
            .Where(Claimable(now))
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, RunStates.Running)
                .SetProperty(x => x.LeaseToken, lease)
                .SetProperty(x => x.LeaseUntil, now.AddSeconds(JobService.LeaseSeconds))
                .SetProperty(x => x.Attempt, x => x.Attempt + 1)
                .SetProperty(x => x.UpdatedAt, now), stop);
        if (claimed != 1) return true;

        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == id, stop);
        if (job.TraceId is null || job.ParentSpanId is null || job.OperationId == Guid.Empty)
        {
            job.TraceId ??= ActivityTraceId.CreateRandom().ToHexString();
            job.ParentSpanId ??= ActivitySpanId.CreateRandom().ToHexString();
            job.OperationId = job.Id;
            await db.Set<BackgroundJob>()
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
            var handler = JobService.HandlerFor(scope.ServiceProvider, job.Kind);
            var outcome = job.Attempt > JobService.MaxAttempts ? JobsErrors.JobRetryLimit
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
            var external = (ex as ExternalServiceException)?.Error;
            code = external?.Code ?? "job_processing_failed";
            issue = issues.Report(ex, code);
            message = external is null ? Issues.Message(issue) : Issues.Message(external, issue);
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
        var finished = await db.Set<BackgroundJob>()
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
            var final = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken.None);
            var isRepositoryReview = final.Kind == "repository-review";
            var (level, title) = final.Status switch
            {
                RunStates.Completed => ("success", "背景任務已完成"),
                RunStates.Failed => ("error", "背景任務需要重試"),
                _ => ("info", "背景任務已取消"),
            };
            await scope.ServiceProvider.GetRequiredService<NotificationService>().PublishAsync(
                final.OwnerId,
                $"job:{id}:{final.Attempt}",
                "task." + final.Status,
                level,
                title,
                final.Status == RunStates.Failed ? final.ErrorMessage ?? Issues.Message(final.IssueCode) : final.Label,
                isRepositoryReview ? "repository-review" : "task",
                isRepositoryReview ? final.SubjectId : final.Id,
                CancellationToken.None,
                final.IssueCode);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        await completion.CommitAsync(CancellationToken.None);
        LogFinished(logger, status);
        return true;
    }

    private async Task MonitorAsync(Guid id, Guid lease, CancellationTokenSource execution, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(2000, ct);
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var count = await db.Set<BackgroundJob>()
                    .Where(x => x.Id == id && x.LeaseToken == lease && x.Status == RunStates.Running && !x.CancelRequested)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseUntil, clock.GetUtcNow().AddSeconds(JobService.LeaseSeconds)), ct);
                if (count == 0)
                {
                    execution.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogLeaseRenewalFailed(logger, id, ex.GetType().Name);
            execution.Cancel();
        }
    }

    // 可認領：排隊中，或執行中但租約已過期（原 worker 停止續租）。挑選與條件式 UPDATE 必須用同一條件。
    private static Expression<Func<BackgroundJob, bool>> Claimable(DateTimeOffset now) =>
        x => x.Status == RunStates.Queued || (x.Status == RunStates.Running && x.LeaseUntil < now);

    [LoggerMessage(EventId = 3000, EventName = "job.started", Level = LogLevel.Information, Message = "Background job started; attempt {Attempt}.")]
    private static partial void LogStarted(ILogger logger, int attempt);

    [LoggerMessage(EventId = 3001, EventName = "job.finished", Level = LogLevel.Information, Message = "Background job finished with {Stage}.")]
    private static partial void LogFinished(ILogger logger, string stage);

    [LoggerMessage(EventId = 3002, EventName = "job.queue_unavailable", Level = LogLevel.Warning, Message = "Background queue unavailable ({ErrorType}).")]
    private static partial void LogQueueUnavailable(ILogger logger, string errorType);

    [LoggerMessage(EventId = 3003, EventName = "job.lease_renewal_failed", Level = LogLevel.Warning, Message = "Job {JobId} lease renewal failed ({ErrorType}).")]
    private static partial void LogLeaseRenewalFailed(ILogger logger, Guid jobId, string errorType);
}
