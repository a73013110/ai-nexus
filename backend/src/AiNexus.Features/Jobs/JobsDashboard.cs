using AiNexus.Features.AccessControl;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Jobs;

/// <summary>
/// Overview of durable background jobs. In progress means queued or running; failed is only <c>failed</c>, never every
/// unfinished job. The kinds in the period form the distribution.
/// </summary>
internal sealed class JobsDashboard(NexusDbContext db) : IDashboardSource
{
    private static readonly Dictionary<string, string> Kinds = new(StringComparer.Ordinal)
    {
        ["document-ingest"] = "文字擷取",
        ["document-embedding"] = "向量索引",
        ["embedding-reindex"] = "重新索引",
        ["evaluation"] = "品質評測",
        ["retrieval-eval"] = "檢索評測",
        ["repository-review"] = "程式碼 review",
    };

    public string Feature => FeatureIds.Tasks;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var jobs = db.Set<BackgroundJob>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var current = await jobs.Where(x => x.Status == "queued" || x.Status == "running" || x.Status == "failed").GroupBy(x => 1).Select(g => new
        {
            Queued = g.Count(x => x.Status == "queued"),
            Running = g.Count(x => x.Status == "running"),
            Failed = g.Count(x => x.Status == "failed"),
        }).SingleOrDefaultAsync(ct);
        var period = jobs.Where(x => x.CreatedAt >= scope.From && x.CreatedAt < scope.Until);
        var completed = await period.CountAsync(x => x.Status == "completed", ct);
        var kinds = await period.GroupBy(x => x.Kind).Select(g => new { Kind = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync(ct);
        var daily = await DashboardDays.CountAsync(db, jobs.Select(x => x.CreatedAt), scope, ct);
        var active = (current?.Queued ?? 0) + (current?.Running ?? 0);
        var failed = current?.Failed ?? 0;
        var section = new DashboardSection("tasks", Feature)
            .Count("active", "處理中任務", active, "件", active > 0 ? DashboardTones.Info : DashboardTones.Neutral,
                $"{current?.Running ?? 0} 件執行中 · {current?.Queued ?? 0} 件等待中")
            .Count("failed", "失敗任務", failed, "件", failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral, "可在背景任務頁重試")
            .Period("created", "期間建立", daily.Sum(x => x.Value), "件")
            .Period("completed", "期間完成", completed, "件", tone: DashboardTones.Success)
            .Daily("created", "每日建立任務", "件", daily)
            .ActiveWhen(active);
        foreach (var kind in kinds) section.Share(kind.Kind, Kinds.GetValueOrDefault(kind.Kind, kind.Kind), kind.Count);
        var mine = await db.Set<BackgroundJob>().AsNoTracking()
            .Where(x => x.OwnerId == scope.Actor && (x.Status == "queued" || x.Status == "running" || x.Status == "failed"))
            .OrderByDescending(x => x.UpdatedAt).Take(6).Select(x => new { x.Id, x.Label, x.Kind, x.Stage, x.Status, x.UpdatedAt }).ToListAsync(ct);
        foreach (var job in mine)
            section.Item(job.Id.ToString(), job.Label.Length > 0 ? job.Label : Kinds.GetValueOrDefault(job.Kind, job.Kind),
                job.Status == "failed" ? "失敗" : job.Stage, job.UpdatedAt, "/tasks?job=" + job.Id,
                job.Status == "failed" ? DashboardTones.Danger : DashboardTones.Info);
        return section.Build();
    }
}
