using AiNexus.BuildingBlocks.Diagnostics;
using System.Diagnostics;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Collaboration;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.Operations;

public sealed class BackgroundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string? TraceId { get; set; }
    public string? ParentSpanId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid SubjectId { get; set; }
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string Stage { get; set; } = "等待處理";
    public string? ActiveKey { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public bool CancelRequested { get; set; }
    public int Attempt { get; set; }
    public int CompletedUnits { get; set; }
    public int? TotalUnits { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed record JobDto(Guid Id, string Kind, Guid SubjectId, string Label, string Status, string Stage, int Attempt, int CompletedUnits, int? TotalUnits, bool CancelRequested, string? ErrorCode, string? ErrorMessage, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? IssueCode = null);
public interface IBackgroundJobHandler
{
    string Kind { get; }
    Task ExecuteAsync(JobExecution execution, CancellationToken ct);
    Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct);
}
public sealed class JobService(NexusDbContext db, IServiceProvider services)
{
    public static JobDto Describe(BackgroundJob x) => new(x.Id, x.Kind, x.SubjectId, x.Label, x.Status, x.Stage, x.Attempt, x.CompletedUnits, x.TotalUnits, x.CancelRequested, x.ErrorCode, x.Status == "failed" ? Issues.Message(x.IssueCode) : null, x.CreatedAt, x.UpdatedAt, x.IssueCode);
    public BackgroundJob Enqueue(Guid owner, Guid? resource, Guid subject, string kind, string label)
    {
        var job = new BackgroundJob { OwnerId = owner, ResourceId = resource, SubjectId = subject, Kind = kind, Label = label, ActiveKey = kind + ":" + subject.ToString("N") };
        job.TraceId = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString(); job.ParentSpanId = Activity.Current?.SpanId.ToHexString() ?? ActivitySpanId.CreateRandom().ToHexString(); job.OperationId = job.Id;
        db.Add(job); return job; // The caller commits subject and job atomically.
    }
    public async Task<IReadOnlyList<JobDto>> ListAsync(Guid owner, CancellationToken ct) => (await db.Set<BackgroundJob>().AsNoTracking().Where(x => x.OwnerId == owner).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct)).Select(Describe).ToList();
    public async Task<BackgroundJob> OwnedAsync(Guid owner, Guid id, CancellationToken ct) => await db.Set<BackgroundJob>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) ?? throw new ApiException(404, "job_not_found", "找不到這個背景任務。");
    public async Task<JobDto> CancelAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await OwnedAsync(owner, id, ct);
        await db.Set<BackgroundJob>().Where(x => x.Id == id && (x.Status == "queued" || x.Status == "running"))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        db.ChangeTracker.Clear(); return Describe(await OwnedAsync(owner, id, ct));
    }
    public async Task<JobDto> RetryAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await OwnedAsync(owner, id, ct);
        if (job.Status is not ("failed" or "cancelled")) throw new ApiException(409, "job_not_retryable", "只有失敗或已取消的任務可以重試。");
        if (job.Attempt >= 6) throw new ApiException(409, "job_retry_limit", "此任務已嘗試六次。請確認設定後重新建立來源。");
        var handler = services.GetServices<IBackgroundJobHandler>().SingleOrDefault(x => x.Kind == job.Kind) ?? throw new ApiException(409, "job_handler_missing", "此任務類型已停用。");
        await handler.ValidateRetryAsync(job, ct);
        var active = job.Kind + ":" + job.SubjectId.ToString("N");
        if (await db.Set<BackgroundJob>().AnyAsync(x => x.ActiveKey == active && x.Id != id, ct)) throw new ApiException(409, "job_active", "同一來源已有處理中的任務。");
        var changed = await db.Set<BackgroundJob>().Where(x => x.Id == id && x.Status == job.Status && x.LeaseToken == null).ExecuteUpdateAsync(p =>
            p.SetProperty(x => x.Status, "queued").SetProperty(x => x.Stage, "等待重試").SetProperty(x => x.ActiveKey, active)
             .SetProperty(x => x.CancelRequested, false).SetProperty(x => x.ErrorCode, (string?)null).SetProperty(x => x.IssueCode, (string?)null).SetProperty(x => x.ErrorMessage, (string?)null).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed != 1) throw new ApiException(409, "job_changed", "任務狀態已改變，請重新載入。");
        db.ChangeTracker.Clear(); return Describe(await OwnedAsync(owner, id, ct));
    }
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
            await scope.ServiceProvider.GetRequiredService<AiNexus.Modules.Notifications.NotificationService>().PublishAsync(final.OwnerId,
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
public static class BackgroundJobConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var job = model.Entity<BackgroundJob>(); job.ToTable("BackgroundJobs", "operations"); job.HasKey(x => x.Id);
        job.Property(x => x.Kind).HasMaxLength(32); job.Property(x => x.Label).HasMaxLength(180); job.Property(x => x.Status).HasMaxLength(16); job.Property(x => x.Stage).HasMaxLength(120);
        job.Property(x => x.IssueCode).HasMaxLength(40); job.Property(x => x.TraceId).HasMaxLength(32); job.Property(x => x.ParentSpanId).HasMaxLength(16);
        job.Property(x => x.ActiveKey).HasMaxLength(100); job.Property(x => x.ErrorCode).HasMaxLength(80); job.Property(x => x.ErrorMessage).HasMaxLength(240);
        job.HasIndex(x => x.ActiveKey).IsUnique().HasFilter("[ActiveKey] IS NOT NULL"); job.HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt }); job.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        job.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        job.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
