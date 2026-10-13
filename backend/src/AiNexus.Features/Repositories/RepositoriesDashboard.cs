using AiNexus.Features.AccessControl;
using AiNexus.Features.Jobs;
using AiNexus.Features.Persistence;
using AiNexus.Features.Repositories.Connections;
using AiNexus.Features.Repositories.Reviews;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Repositories;

/// <summary>Overview of Gitea use: connected accounts, AI reviews started in the period and those still running.</summary>
internal sealed class RepositoriesDashboard(NexusDbContext db, IOptions<GiteaOptions> gitea) : IDashboardSource
{
    public string Feature => FeatureIds.Repositories;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var section = new DashboardSection("repositories", Feature)
            .Fact("Gitea", gitea.Value.Enabled ? "已啟用" : "待設定", gitea.Value.Enabled ? DashboardTones.Success : DashboardTones.Warning);
        if (!gitea.Value.Enabled) return section.Build();
        var reviews = db.Set<RepositoryReview>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var jobs = db.Set<BackgroundJob>().AsNoTracking();
        var status = await reviews.Where(r => r.CreatedAt >= scope.From && r.CreatedAt < scope.Until)
            .Select(r => jobs.Where(j => j.Id == r.JobId).Select(j => j.Status).FirstOrDefault() ?? "completed")
            .GroupBy(x => x).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var active = await reviews.CountAsync(r => jobs.Any(j => j.Id == r.JobId && (j.Status == "queued" || j.Status == "running")), ct);
        var daily = await DashboardDays.CountAsync(db, reviews.Select(x => x.CreatedAt), scope, ct);
        var failed = status.Where(x => x.Status == "failed").Sum(x => x.Count);
        var connections = await db.Set<RepositoryConnection>().AsNoTracking().CountAsync(x => owner == null || x.OwnerId == owner, ct);
        if (owner == null) section.Count("connections", "已連線帳號", connections, "個");
        else section.Fact("帳號連線", connections > 0 ? "已連線" : "未連線", connections > 0 ? DashboardTones.Success : DashboardTones.Neutral);
        section
            .Count("running", "Review 進行中", active, "次", active > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Period("reviews", "期間 Review", daily.Sum(x => x.Value), "次")
            .Period("failed", "Review 失敗", failed, "次", tone: failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Daily("reviews", "每日 AI review", "次", daily)
            .Share("completed", "完成", status.Where(x => x.Status == "completed").Sum(x => x.Count), DashboardTones.Success)
            .Share("active", "進行中", status.Where(x => x.Status is "queued" or "running").Sum(x => x.Count), DashboardTones.Info)
            .Share("failed", "失敗", failed, DashboardTones.Danger)
            .Share("cancelled", "已取消", status.Where(x => x.Status == "cancelled").Sum(x => x.Count))
            .ActiveWhen(active);
        var recent = await reviews.Where(x => x.OwnerId == scope.Actor).OrderByDescending(x => x.CreatedAt).Take(5)
            .Select(x => new { x.Id, x.Repository, x.Commit, x.CreatedAt }).ToListAsync(ct);
        foreach (var review in recent)
            section.Item(review.Id.ToString(), review.Repository, review.Commit[..Math.Min(7, review.Commit.Length)], review.CreatedAt, "/repositories/" + review.Repository + "/reviews/" + review.Id);
        return section.Build();
    }
}
