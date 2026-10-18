using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Admin.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

/// <summary>使用者最新的 100 筆背景工作。</summary>
internal sealed class ListJobs(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, ListJobs handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListJobs");

    public async Task<IReadOnlyList<JobDto>> HandleAsync(Guid owner, CancellationToken ct)
    {
        var jobs = await db.Set<BackgroundJob>().AsNoTracking()
            .OwnedBy(owner)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return jobs.Select(JobService.Describe).ToList();
    }
}
