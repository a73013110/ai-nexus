using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 持久化背景工作的執行期監控，涵蓋所有擁有者。執行中工作的租約已過期（worker 停止續租）或最近 1 小時內有工作失敗，即為 degraded。
/// 查詢會走 (Status, LeaseUntil, CreatedAt) 索引。
/// </summary>
internal sealed class JobsMonitoring(AppDbContext db, TimeProvider clock) : IMonitoringSource
{
    public string Id => "jobs";
    public string Name => "背景工作";
    public int Order => 30;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hour = now.AddHours(-1);
        var day = now.AddDays(-1);
        var jobs = db.Set<BackgroundJob>().AsNoTracking();
        var active = await jobs.Where(x => x.Status == RunStates.Queued || x.Status == RunStates.Running).GroupBy(x => 1).Select(g => new
        {
            Queued = g.Count(x => x.Status == RunStates.Queued),
            Running = g.Count(x => x.Status == RunStates.Running),
            Stuck = g.Count(x => x.Status == RunStates.Running && x.LeaseUntil < now),
        }).SingleOrDefaultAsync(ct);
        var oldest = await jobs.Where(x => x.Status == RunStates.Queued).OrderBy(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefaultAsync(ct);
        // 失敗工作的 UpdatedAt 即為失敗時間。
        var failed = await jobs.Where(x => x.Status == RunStates.Failed && x.UpdatedAt >= day).GroupBy(x => 1).Select(g => new
        {
            Day = g.Count(),
            Hour = g.Count(x => x.UpdatedAt >= hour),
        }).SingleOrDefaultAsync(ct);
        var queued = active?.Queued ?? 0;
        var running = active?.Running ?? 0;
        var stuck = active?.Stuck ?? 0;
        var failedDay = failed?.Day ?? 0;
        var failedHour = failed?.Hour ?? 0;
        var waited = oldest is { } created ? Math.Max(0, Math.Round((now - created).TotalSeconds)) : (double?)null;
        var (status, summary) =
            stuck > 0 ? (SubsystemStates.Degraded, $"{stuck} 個執行中的工作租約已過期，可能已停止。")
            : failedHour > 0 ? (SubsystemStates.Degraded, $"最近 1 小時有 {failedHour} 個工作失敗。")
            : queued + running == 0 ? (SubsystemStates.Idle, "目前沒有等待或執行中的工作。")
            : (SubsystemStates.Healthy, $"{running} 個執行中，{queued} 個等待中。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("queued", "等待中", queued),
            SubsystemFact.Count("running", "執行中", running, running > 0 ? SubsystemTones.Positive : SubsystemTones.Neutral),
            SubsystemFact.Count("failed_24h", "24 小時內失敗", failedDay, failedDay > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("oldest_queued", "最久等待", waited, waited > 600 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("expired_leases", "租約過期", stuck, stuck > 0 ? SubsystemTones.Danger : SubsystemTones.Neutral),
        ];
        return new(status, summary, facts);
    }
}
