using AiNexus.Platform.Diagnostics;
using System.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Operations;

public interface IBackgroundJobHandler
{
    string Kind { get; }
    Task ExecuteAsync(JobExecution execution, CancellationToken ct);
    Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct);
}
// Every persisted checkpoint is fenced by the current lease in the same transaction.
public sealed class JobExecution(NexusDbContext db, BackgroundJob job, Guid lease)
{
    public BackgroundJob Job => job;
    public NexusDbContext Database => db;
    public async Task CheckpointAsync(string stage, int completed, int? total, CancellationToken ct, Func<Task>? afterSave = null, System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted)
    {
        ct.ThrowIfCancellationRequested();
        await using var transaction = await db.Database.BeginTransactionAsync(isolation, ct);
        var changed = await db.Set<BackgroundJob>().Where(x => x.Id == job.Id && x.LeaseToken == lease && x.Status == "running" && !x.CancelRequested)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Stage, stage).SetProperty(x => x.CompletedUnits, completed).SetProperty(x => x.TotalUnits, total).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow).SetProperty(x => x.LeaseUntil, DateTimeOffset.UtcNow.AddSeconds(60)), ct);
        if (changed != 1) throw new OperationCanceledException("Job lease lost or cancellation requested.", ct);
        await db.SaveChangesAsync(ct);
        if (afterSave is not null) await afterSave();
        await transaction.CommitAsync(ct);
    }
}

public sealed class BackgroundJobWorker(IServiceScopeFactory scopes, StorageReadiness readiness, ILogger<BackgroundJobWorker> logger, Issues issues) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!readiness.Configured || !await ProcessNextAsync(stoppingToken)) await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Background queue unavailable ({ErrorType}).", ex.GetType().Name); await Task.Delay(3000, stoppingToken); }
        }
    }
    public async Task<bool> ProcessNextAsync(CancellationToken stop)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidate = await db.Set<BackgroundJob>().AsNoTracking().Where(x => x.Status == "queued" || (x.Status == "running" && x.LeaseUntil < now)).OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(stop);
        if (candidate is not Guid id) return false;
        var lease = Guid.NewGuid();
        var claimed = await db.Set<BackgroundJob>().Where(x => x.Id == id && (x.Status == "queued" || (x.Status == "running" && x.LeaseUntil < now)))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "running").SetProperty(x => x.LeaseToken, lease).SetProperty(x => x.LeaseUntil, now.AddSeconds(60)).SetProperty(x => x.Attempt, x => x.Attempt + 1).SetProperty(x => x.UpdatedAt, now), stop);
        if (claimed != 1) return true;
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == id, stop);
        if (job.TraceId is null || job.ParentSpanId is null || job.OperationId == Guid.Empty)
        {
            job.TraceId ??= ActivityTraceId.CreateRandom().ToHexString(); job.ParentSpanId ??= ActivitySpanId.CreateRandom().ToHexString(); job.OperationId = job.Id;
            await db.Set<BackgroundJob>().Where(x => x.Id == id && x.LeaseToken == lease).ExecuteUpdateAsync(p => p.SetProperty(x => x.TraceId, job.TraceId).SetProperty(x => x.ParentSpanId, job.ParentSpanId).SetProperty(x => x.OperationId, job.OperationId), stop);
        }
        using var activity = DiagnosticTrace.Start("job.execute", job.TraceId, job.ParentSpanId, ActivityKind.Consumer);
        activity.SetTag("operation.id", job.OperationId.ToString());
        using var logging = logger.BeginScope(new Dictionary<string, object?> { ["JobId"] = id, ["OperationId"] = job.OperationId, ["UserId"] = job.OwnerId, ["Attempt"] = job.Attempt, ["Kind"] = job.Kind, ["RequestId"] = null });
        logger.LogInformation(DiagnosticEvents.JobStarted, "Background job started; attempt {Attempt}.", job.Attempt);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(stop);
        using var monitor = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var heartbeat = MonitorAsync(id, lease, execution, monitor.Token);
        var status = "completed"; string? code = null, message = null, issue = null;
        try
        {
            if (job.CancelRequested) { execution.Cancel(); execution.Token.ThrowIfCancellationRequested(); }
            if (job.Attempt > 6) throw new ApiException(409, "job_retry_limit", "任務重試次數已達上限。");
            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().SingleOrDefault(x => x.Kind == job.Kind) ?? throw new ApiException(409, "job_handler_missing", "任務類型目前無法處理。");
            await handler.ExecuteAsync(new(db, job, lease), execution.Token);
        }
        catch (OperationCanceledException) { status = "cancelled"; }
        catch (Exception ex)
        {
            status = "failed"; code = (ex as ApiException)?.Code ?? "job_processing_failed";
            issue = issues.Report(ex, code); message = Issues.Message(issue);
        }
        finally { monitor.Cancel(); try { await heartbeat; } catch (OperationCanceledException) { } }
        // Host shutdown keeps the lease and checkpoint so another process can resume later.
        if (stop.IsCancellationRequested) return true;
        // Only a fenced checkpoint may persist handler changes. Discard unfinished tracked work
        // before recording the terminal state and its notification in a separate transaction.
        db.ChangeTracker.Clear();
        await using var completion = await db.Database.BeginTransactionAsync(CancellationToken.None);
        var finished = await db.Set<BackgroundJob>().Where(x => x.Id == id && x.LeaseToken == lease && x.Status == "running")
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, x => x.CancelRequested ? "cancelled" : status).SetProperty(x => x.Stage, x => x.CancelRequested ? "已取消" : status == "completed" ? "處理完成" : status == "cancelled" ? "已取消" : "需要重試")
             .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.ActiveKey, (string?)null)
             .SetProperty(x => x.ErrorCode, code).SetProperty(x => x.IssueCode, issue).SetProperty(x => x.ErrorMessage, message).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), CancellationToken.None);
        if (finished == 1) {
            var final = await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(x => x.Id == id);
            await scope.ServiceProvider.GetRequiredService<AiNexus.Features.Notifications.NotificationService>().PublishAsync(final.OwnerId,
                $"job:{id}:{final.Attempt}", "task." + final.Status, final.Status == "failed" ? "error" : final.Status == "completed" ? "success" : "info",
                final.Status == "completed" ? "背景任務已完成" : final.Status == "failed" ? "背景任務需要重試" : "背景任務已取消",
                final.Status == "failed" ? Issues.Message(final.IssueCode) : final.Label, final.Kind == "repository-review" ? "repository-review" : "task", final.Kind == "repository-review" ? final.SubjectId : final.Id, CancellationToken.None, final.IssueCode);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        await completion.CommitAsync(CancellationToken.None);
        logger.LogInformation(DiagnosticEvents.JobFinished, "Background job finished with {Stage}.", status);
        return true;
    }
    private async Task MonitorAsync(Guid id, Guid lease, CancellationTokenSource execution, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(2000, ct);
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                var count = await db.Set<BackgroundJob>().Where(x => x.Id == id && x.LeaseToken == lease && x.Status == "running" && !x.CancelRequested)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseUntil, DateTimeOffset.UtcNow.AddSeconds(60)), ct);
                if (count == 0) { execution.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogWarning("Job {JobId} lease renewal failed ({ErrorType}).", id, ex.GetType().Name); execution.Cancel(); }
    }
}
