using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Operations;

/// <summary>One of the user's background jobs.</summary>
internal static class GetJob
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
            await db.Set<BackgroundJob>().AsNoTracking().OwnedBy(user.Id).SingleOrDefaultAsync(x => x.Id == id, ct) is { } job
                ? Results.Ok(JobService.Describe(job)) : OperationsErrors.JobNotFound.ToProblem())
        .WithName("GetJob").Produces<JobDto>();
}
