using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

/// <summary>Asks a queued or running job of the user to stop; the worker finishes it as cancelled.</summary>
internal sealed class CancelJob(NexusDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/cancel", async (Guid id, ICurrentUser user, CancelJob handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("CancelJob").Produces<JobDto>();

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (await db.Set<BackgroundJob>().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct) is null) return JobsErrors.JobNotFound;
        await db.Set<BackgroundJob>().Where(x => x.Id == id && (x.Status == "queued" || x.Status == "running"))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true).SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
        return await db.ReloadJobAsync(owner, id, ct);
    }
}
