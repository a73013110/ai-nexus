using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs.Queue;

/// <summary>
/// 管理員停止任何人排隊中或執行中的工作，例如卡住或拖慢整個佇列的那一筆。與使用者自己停止相同：只要求停止，由 worker 收尾為 cancelled；
/// 已結束的工作是無作用的成功。擁有者仍可在自己的背景任務頁重試。
/// </summary>
internal sealed class CancelQueuedJob(AppDbContext db, AuditTrail audit, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/cancel", (Guid id, CancelQueuedJob handler, CancellationToken ct) => handler.HandleAsync(id, ct))
        .RequireFeature(FeatureCatalog.SchedulingManage)
        .Audited(JobsAudit.QueueJobCancelled)
        .WithName("CancelQueuedJob");

    public async Task<Result<QueuedJobDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        var owner = await db.BackgroundJobs.Where(x => x.Id == id).Select(x => (Guid?)x.OwnerId).SingleOrDefaultAsync(ct);
        if (owner is null) return JobsErrors.JobNotFound;
        audit.Describe(resource: id, subject: owner);
        await db.BackgroundJobs.Where(x => x.Id == id).RequestCancelAsync(clock.GetUtcNow(), ct);
        db.ChangeTracker.Clear();
        return await db.BackgroundJobs.AsNoTracking().Where(x => x.Id == id).ToQueuedDtos(db).SingleAsync(ct);
    }
}
