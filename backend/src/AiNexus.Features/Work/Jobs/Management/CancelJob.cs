using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs.Management;

/// <summary>要求停止使用者排隊中或執行中的工作；實際停止由 worker 完成，並收尾為 cancelled。</summary>
internal sealed class CancelJob(AppDbContext db, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/cancel", (Guid id, ICurrentUser user, CancelJob handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct))
        .Audited(JobsAudit.JobCancelled)
        .WithName("CancelJob");

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (!await db.BackgroundJobs.OwnedBy(owner).AnyAsync(x => x.Id == id, ct)) return JobsErrors.JobNotFound;
        await db.BackgroundJobs.Where(x => x.Id == id).RequestCancelAsync(clock.GetUtcNow(), ct);
        return await db.ReloadJobAsync(owner, id, ct);
    }
}
