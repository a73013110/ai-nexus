using AiNexus.Data.App;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>
/// 排程任務的監控。有任務正在失敗、執行中卻租約過期（程序中止），或到期超過 <see cref="OverdueGrace"/> 還沒人執行
/// （所有程序都關了 <c>Scheduling:Execute</c> 或排程迴圈停了）即為 degraded。只讀任務列（每個任務一列），不掃執行紀錄。
/// </summary>
internal sealed class SchedulingMonitoring(AppDbContext db, ScheduledTaskCatalog catalog, IServiceProvider services, TimeProvider clock) : IMonitoringSource
{
    public static readonly TimeSpan OverdueGrace = TimeSpan.FromMinutes(2);

    public string Id => "scheduled-tasks";
    public string Name => "排程任務";
    public int Order => 31;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var runnable = catalog.All.Where(x => x.UnavailableReason(services) is null).Select(x => x.Key).ToList();
        var rows = await db.ScheduledTasks.AsNoTracking().Where(x => runnable.Contains(x.Key))
            .Select(x => new { x.IsPaused, x.NextRunAt, x.LeaseToken, x.LeaseUntil, x.ConsecutiveFailures }).ToListAsync(ct);
        var running = rows.Count(x => x.LeaseUntil > now);
        var stuck = rows.Count(x => x.LeaseToken != null && x.LeaseUntil <= now);
        var failing = rows.Count(x => x.ConsecutiveFailures > 0);
        var paused = rows.Count(x => x.IsPaused);
        var overdue = rows.Count(x => !x.IsPaused && x.LeaseToken == null && x.NextRunAt < now - OverdueGrace);
        var (status, summary) =
            overdue > 0 ? (SubsystemStates.Degraded, $"{overdue} 個任務已過執行時間卻沒有執行，請確認有程序開啟 Scheduling:Execute。")
            : stuck > 0 ? (SubsystemStates.Degraded, $"{stuck} 個任務執行中斷，等待其他程序接手。")
            : failing > 0 ? (SubsystemStates.Degraded, $"{failing} 個任務最近一次執行失敗。")
            : (SubsystemStates.Healthy, running > 0 ? $"{running} 個任務執行中。" : $"{rows.Count} 個任務依排程執行中。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("tasks", "任務", catalog.All.Count),
            SubsystemFact.Count("running", "執行中", running, running > 0 ? SubsystemTones.Positive : SubsystemTones.Neutral),
            SubsystemFact.Count("failing", "失敗中", failing, failing > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Count("paused", "已暫停", paused),
            SubsystemFact.Count("overdue", "逾期未執行", overdue, overdue > 0 ? SubsystemTones.Danger : SubsystemTones.Neutral),
        ];
        // 任務分批提交且可重跑：停站時執行中的那一輪記為中斷，重啟後重跑，只是晚一點完成。
        return new(status, summary, facts, new StopImpact(0, running));
    }
}
