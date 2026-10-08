using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Operations;

/// <summary>The user's 100 newest background jobs.</summary>
internal static class ListJobs
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
            Results.Ok((await db.Set<BackgroundJob>().AsNoTracking().OwnedBy(user.Id).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct)).Select(JobService.Describe).ToList()))
        .WithName("ListJobs").Produces<IReadOnlyList<JobDto>>();
}
