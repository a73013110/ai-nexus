using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs.Management;

/// <summary>
/// 把使用者的失敗或已取消工作重新排入佇列：工作種類仍有處理器、處理器接受重試，且同一業務對象沒有其他進行中的工作。
/// 不限次數：失敗多半來自外部服務暫時異常，恢復後使用者要能就地重試，而不是重建業務對象。更新以讀到的狀態作為條件（fencing），避免覆寫併發的狀態變更。
/// </summary>
internal sealed class RetryJob(AppDbContext db, KeyedServices<IBackgroundJobHandler> handlers, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retry", (Guid id, ICurrentUser user, RetryJob handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(JobsAudit.JobRetried)
        .WithName("RetryJob");

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await db.BackgroundJobs.OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return JobsErrors.JobNotFound;
        if (job.Status is not (RunStates.Failed or RunStates.Cancelled)) return JobsErrors.JobNotRetryable;
        // 只有可重試的工作才解析處理器；處理器自己的檢查屬於其所在模組，這裡不重複。
        var handler = handlers.Find(job.Kind);
        if (handler is null) return JobsErrors.JobHandlerMissing;
        var allowed = await handler.ValidateRetryAsync(job, ct);
        if (!allowed.IsSuccess) return allowed.Error;
        var active = JobService.ActiveKey(job.Kind, job.SubjectId);
        if (await db.BackgroundJobs.AnyAsync(x => x.ActiveKey == active && x.Id != id, ct)) return JobsErrors.JobActive;
        // 條件中的 Status 即為讀到的狀態（fencing）：若在此之間被別人改過，UPDATE 不會命中。
        var changed = await db.BackgroundJobs
            .Where(x => x.Id == id && x.Status == job.Status && x.LeaseToken == null)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, RunStates.Queued)
                .SetProperty(x => x.Stage, "等待重試")
                .SetProperty(x => x.ActiveKey, active)
                .SetProperty(x => x.CancelRequested, false)
                .SetProperty(x => x.ErrorCode, (string?)null)
                .SetProperty(x => x.IssueCode, (string?)null)
                .SetProperty(x => x.ErrorMessage, (string?)null)
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
        if (changed != 1) return JobsErrors.JobChanged;
        return await db.ReloadJobAsync(owner, id, ct);
    }
}
