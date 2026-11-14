using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>
/// Google 日曆同步的監控。未啟用 idle；最近一輪無法執行（GD、憑證、權限、日曆）failing；有推送失敗的事件、安全閥擋下移除，
/// 或待同步的事件放超過兩輪 degraded；其餘 healthy。最近一輪讀排程任務的執行紀錄，所以不論哪個程序執行都看得到。
/// 數字走 <c>ix_CalendarEvents_CalendarId_NextAttemptAt</c>（只含待同步與失敗的列），不掃整張表。
/// </summary>
internal sealed class CalendarSyncMonitoring(AppDbContext db, ScheduledTasks tasks, IOptions<CalendarSyncOptions> options, TimeProvider clock) : IMonitoringSource
{
    public string Id => "calendar-sync";
    public string Name => "Google 日曆同步";
    public int Order => 39;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return new(SubsystemStates.Idle, "Google 日曆同步未啟用。", []);

        var now = clock.GetUtcNow();
        var queue = await db.CalendarEvents.AsNoTracking()
            .Where(x => x.CalendarId == settings.CalendarId && (x.State == CalendarEventStates.Pending || x.State == CalendarEventStates.Failed))
            .GroupBy(x => x.State).Select(x => new { State = x.Key, Count = x.Count(), Oldest = x.Min(e => e.UpdatedAt) }).ToListAsync(ct);
        var failed = queue.FirstOrDefault(x => x.State == CalendarEventStates.Failed)?.Count ?? 0;
        var pending = queue.FirstOrDefault(x => x.State == CalendarEventStates.Pending);
        // 每輪最多推 MaxPushesPerRun 筆：大量待同步（第一次轉入）要好幾輪才推完，容許的時間跟著輪數放寬，正常排隊不算卡住。
        var rounds = pending is null ? 0 : (pending.Count + settings.MaxPushesPerRun - 1) / settings.MaxPushesPerRun;
        var progress = await CalendarSyncChecks.ReadAsync(tasks, db, settings.CalendarId, ct);
        var stale = pending is not null && now - pending.Oldest > progress.Interval * (rounds + 1) + TimeSpan.FromMinutes(1);
        var check = progress.LastCheck;
        double? sinceCheck = check is null ? null : Math.Max(0, Math.Round((now - check.At).TotalSeconds));

        var (status, summary) =
            check is { Status: SyncRunStatuses.Failed } ? (SubsystemStates.Failing, $"最近一輪同步無法完成（{check.ErrorCode}），請到日曆同步頁查看原因。")
            : check is { HeldCount: > 0 } ? (SubsystemStates.Degraded, $"{check.HeldCount} 個事件待移除，超過安全閥門檻，等待管理員確認。")
            : failed > 0 ? (SubsystemStates.Degraded, $"{failed} 個事件推送失敗，會自動重試。詳見日曆同步頁。")
            : stale ? (SubsystemStates.Degraded, "有事件等待同步超過兩輪，請確認背景同步是否正常執行。")
            : check is null ? (SubsystemStates.Healthy, "等待第一輪同步。")
            : (SubsystemStates.Healthy, "Google 日曆同步正常。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("pending", "待同步", pending?.Count ?? 0),
            SubsystemFact.Count("failed", "推送失敗", failed, failed > 0 ? SubsystemTones.Warning : SubsystemTones.Neutral),
            SubsystemFact.Seconds("since_check", "距上次檢查", sinceCheck),
        ];
        // 待同步的事件存在資料庫，停站後下一輪接著推；正在進行的一輪中斷也只是晚一點完成（推送是冪等的），所以都是 Resumes。
        return new(status, summary, facts, new StopImpact(0, (pending?.Count ?? 0) + failed));
    }
}
