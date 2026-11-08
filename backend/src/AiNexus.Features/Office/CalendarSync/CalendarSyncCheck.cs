using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Office.CalendarSync.Sync;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.CalendarSync;

/// <summary>
/// 最近一次檢查（每一輪，不論有沒有異動）的結果。<see cref="Status"/> 用同步紀錄的狀態值（succeeded、failed、held，或停止時的排程狀態）。
/// </summary>
public sealed record CalendarSyncCheck(DateTimeOffset At, string Status, string? ErrorCode, int HeldCount);

/// <summary>同步任務目前的狀態：是否執行中或排隊中、間隔與最近一次檢查。資料在排程任務的表，跨程序都看得到。</summary>
internal sealed record CalendarSyncProgress(bool Running, TimeSpan Interval, CalendarSyncCheck? LastCheck);

internal static class CalendarSyncChecks
{
    /// <summary>
    /// 沒有異動的排程輪不寫同步紀錄，最近檢查要看排程任務的執行紀錄。擋下的數量只寫在問題出現那一輪的同步紀錄，這裡從那一筆讀。
    /// </summary>
    public static async Task<CalendarSyncProgress> ReadAsync(ScheduledTasks tasks, AppDbContext db, string calendarId, CancellationToken ct)
    {
        var state = await tasks.GetStateAsync(CalendarSyncTask.Definition.Key, ct);
        var interval = state?.Interval ?? TimeSpan.FromMinutes(5);
        if (state?.LastRun is not { } last) return new(state is { Running: true } or { Requested: true }, interval, null);

        var status = last.Status switch
        {
            ScheduledTaskRunStatuses.Failed when last.ErrorCode == CalendarSyncFailures.RemovalsHeld => SyncRunStatuses.Held,
            ScheduledTaskRunStatuses.Failed => SyncRunStatuses.Failed,
            ScheduledTaskRunStatuses.Succeeded or ScheduledTaskRunStatuses.Idle => SyncRunStatuses.Succeeded,
            _ => last.Status,
        };
        var held = status == SyncRunStatuses.Held
            ? await db.SyncRuns.AsNoTracking().Where(x => x.CalendarId == calendarId).OrderByDescending(x => x.StartedAt)
                .Select(x => x.HeldCount).FirstOrDefaultAsync(ct)
            : 0;
        var check = new CalendarSyncCheck(last.FinishedAt ?? last.StartedAt, status, status == SyncRunStatuses.Failed ? last.ErrorCode : null, held);
        return new(state.Running || state.Requested, interval, check);
    }
}
