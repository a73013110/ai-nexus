using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

internal static class BackgroundJobQueries
{
    public static IQueryable<BackgroundJob> OwnedBy(this IQueryable<BackgroundJob> jobs, Guid owner) => jobs.Where(x => x.OwnerId == owner);

    /// <summary>Re-reads a job after a bulk update; tracked entities are discarded first.</summary>
    public static async Task<Result<JobDto>> ReloadJobAsync(this NexusDbContext db, Guid owner, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var job = await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? JobsErrors.JobNotFound : JobService.Describe(job);
    }
}
