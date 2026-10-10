using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

/// <summary>The user's 100 newest background jobs.</summary>
internal sealed class ListJobs(NexusDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, ListJobs handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListJobs");

    public async Task<IReadOnlyList<JobDto>> HandleAsync(Guid owner, CancellationToken ct)
        => (await db.Set<BackgroundJob>().AsNoTracking().OwnedBy(owner).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct)).Select(JobService.Describe).ToList();
}
