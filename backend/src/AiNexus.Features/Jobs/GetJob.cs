using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

/// <summary>使用者的單一背景工作。</summary>
internal sealed class GetJob(NexusDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetJob handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetJob");

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
        => await db.Set<BackgroundJob>().AsNoTracking().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct) is { } job
            ? JobService.Describe(job) : JobsErrors.JobNotFound;
}
