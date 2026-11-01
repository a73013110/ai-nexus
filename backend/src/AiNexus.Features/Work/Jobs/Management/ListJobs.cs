using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs.Management;

/// <summary>使用者最新的 100 筆背景工作。</summary>
internal sealed class ListJobs(AppDbContext db)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (ICurrentUser user, ListJobs handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .Audited(JobsAudit.JobRead)
        .WithName("ListJobs");

    public async Task<IReadOnlyList<JobDto>> HandleAsync(Guid owner, CancellationToken ct)
    {
        var jobs = await db.BackgroundJobs.AsNoTracking()
            .OwnedBy(owner)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return jobs.Select(JobService.Describe).ToList();
    }
}
