using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

/// <summary>要求停止使用者排隊中或執行中的工作；實際停止由 worker 完成，並收尾為 cancelled。</summary>
internal sealed class CancelJob(AppDbContext db, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/cancel", (Guid id, ICurrentUser user, CancelJob handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("CancelJob");

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        if (!await db.Set<BackgroundJob>().OwnedBy(owner).AnyAsync(x => x.Id == id, ct)) return JobsErrors.JobNotFound;
        await db.Set<BackgroundJob>().Where(x => x.Id == id && (x.Status == RunStates.Queued || x.Status == RunStates.Running))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.CancelRequested, true).SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
        return await db.ReloadJobAsync(owner, id, ct);
    }
}
