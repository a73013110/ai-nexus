using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

/// <summary>
/// 把使用者的失敗或已取消工作重新排入佇列。全部嘗試上限為 6 次；且工作種類仍有處理器、處理器接受重試，
/// 同一業務對象沒有其他進行中的工作。更新以讀到的狀態作為條件（fencing），避免覆寫併發的狀態變更。
/// </summary>
internal sealed class RetryJob(NexusDbContext db, IServiceProvider services, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retry", (Guid id, ICurrentUser user, RetryJob handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("RetryJob");

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return JobsErrors.JobNotFound;
        if (job.Status is not ("failed" or "cancelled")) return JobsErrors.JobNotRetryable;
        if (job.Attempt >= 6) return JobsErrors.JobRetryLimit;
        // 只有可重試的工作才解析處理器；處理器自己的檢查屬於其所在模組，這裡不重複。
        var handler = services.GetServices<IBackgroundJobHandler>().SingleOrDefault(x => x.Kind == job.Kind);
        if (handler is null) return JobsErrors.JobHandlerMissing;
        var allowed = await handler.ValidateRetryAsync(job, ct);
        if (!allowed.IsSuccess) return allowed.Error;
        var active = job.Kind + ":" + job.SubjectId.ToString("N");
        if (await db.Set<BackgroundJob>().AnyAsync(x => x.ActiveKey == active && x.Id != id, ct)) return JobsErrors.JobActive;
        var changed = await db.Set<BackgroundJob>().Where(x => x.Id == id && x.Status == job.Status && x.LeaseToken == null).ExecuteUpdateAsync(p =>
            p.SetProperty(x => x.Status, "queued").SetProperty(x => x.Stage, "等待重試").SetProperty(x => x.ActiveKey, active)
             .SetProperty(x => x.CancelRequested, false).SetProperty(x => x.ErrorCode, (string?)null).SetProperty(x => x.IssueCode, (string?)null).SetProperty(x => x.ErrorMessage, (string?)null).SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
        if (changed != 1) return JobsErrors.JobChanged;
        return await db.ReloadJobAsync(owner, id, ct);
    }
}
