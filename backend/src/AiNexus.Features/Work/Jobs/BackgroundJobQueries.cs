using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

internal static class BackgroundJobQueries
{
    public static IQueryable<BackgroundJob> OwnedBy(this IQueryable<BackgroundJob> jobs, Guid owner) => jobs.Where(x => x.OwnerId == owner);

    /// <summary>要求停止其中排隊中或執行中的工作；實際停止由 worker 完成並收尾為 cancelled，已結束的工作不受影響。</summary>
    public static Task<int> RequestCancelAsync(this IQueryable<BackgroundJob> jobs, DateTimeOffset now, CancellationToken ct) => jobs
        .Where(x => x.Status == RunStates.Queued || x.Status == RunStates.Running)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true).SetProperty(x => x.UpdatedAt, now), ct);

    /// <summary>批次更新（ExecuteUpdate）後重新讀取工作；先清空 ChangeTracker，否則會拿到更新前的追蹤實體。</summary>
    public static async Task<Result<JobDto>> ReloadJobAsync(this AppDbContext db, Guid owner, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var job = await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? JobsErrors.JobNotFound : JobService.Describe(job);
    }
}
