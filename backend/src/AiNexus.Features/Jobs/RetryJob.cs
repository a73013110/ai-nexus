using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

/// <summary>
/// Queues a failed or cancelled job of the user again, at most six attempts in all, when its kind is still handled, the
/// handler accepts the retry and no other job is active for the same subject. The update is fenced on the read status.
/// </summary>
internal sealed class RetryJob(NexusDbContext db, IServiceProvider services, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/retry", async (Guid id, ICurrentUser user, RetryJob handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("RetryJob").Produces<JobDto>();

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return JobsErrors.JobNotFound;
        if (job.Status is not ("failed" or "cancelled")) return JobsErrors.JobNotRetryable;
        if (job.Attempt >= 6) return JobsErrors.JobRetryLimit;
        // Handlers are resolved only for a retryable job; their own checks belong to their module and fail by exception.
        var handler = services.GetServices<IBackgroundJobHandler>().SingleOrDefault(x => x.Kind == job.Kind);
        if (handler is null) return JobsErrors.JobHandlerMissing;
        await handler.ValidateRetryAsync(job, ct);
        var active = job.Kind + ":" + job.SubjectId.ToString("N");
        if (await db.Set<BackgroundJob>().AnyAsync(x => x.ActiveKey == active && x.Id != id, ct)) return JobsErrors.JobActive;
        var changed = await db.Set<BackgroundJob>().Where(x => x.Id == id && x.Status == job.Status && x.LeaseToken == null).ExecuteUpdateAsync(p =>
            p.SetProperty(x => x.Status, "queued").SetProperty(x => x.Stage, "等待重試").SetProperty(x => x.ActiveKey, active)
             .SetProperty(x => x.CancelRequested, false).SetProperty(x => x.ErrorCode, (string?)null).SetProperty(x => x.IssueCode, (string?)null).SetProperty(x => x.ErrorMessage, (string?)null).SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
        if (changed != 1) return JobsErrors.JobChanged;
        return await db.ReloadJobAsync(owner, id, ct);
    }
}
