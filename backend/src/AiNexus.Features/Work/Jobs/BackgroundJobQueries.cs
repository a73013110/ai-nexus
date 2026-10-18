using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

internal static class BackgroundJobQueries
{
    public static IQueryable<BackgroundJob> OwnedBy(this IQueryable<BackgroundJob> jobs, Guid owner) => jobs.Where(x => x.OwnerId == owner);

    /// <summary>批次更新（ExecuteUpdate）後重新讀取工作；先清空 ChangeTracker，否則會拿到更新前的追蹤實體。</summary>
    public static async Task<Result<JobDto>> ReloadJobAsync(this AppDbContext db, Guid owner, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var job = await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? JobsErrors.JobNotFound : JobService.Describe(job);
    }
}
