using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs.Management;

/// <summary>使用者的單一背景工作。</summary>
internal sealed class GetJob(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetJob handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .Audited(JobsAudit.JobRead)
        .WithName("GetJob");

    public async Task<Result<JobDto>> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var job = await db.Set<BackgroundJob>().AsNoTracking().OwnedBy(owner).SingleOrDefaultAsync(x => x.Id == id, ct);
        return job is null ? JobsErrors.JobNotFound : JobService.Describe(job);
    }
}
