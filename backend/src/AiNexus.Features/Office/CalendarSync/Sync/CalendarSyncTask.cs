using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>手動同步帶給任務的參數（<see cref="Management.StartCalendarSyncRun"/>）。</summary>
public sealed record CalendarSyncParameters(bool ConfirmRemovals);

/// <summary>
/// 定期跑一輪 <see cref="CalendarSyncRunner"/>；管理頁的「立即同步」經排程任務的手動要求提前執行，並帶上是否確認移除。
/// <c>CalendarSync:Enabled</c> 為 false 時不執行（手動同步的端點也會拒絕）。無法完成（GD、憑證、權限、日曆）與安全閥擋下移除都算失敗：
/// 都需要人處理，連續失敗由排程通知管理員。
/// </summary>
public sealed class CalendarSyncTask(CalendarSyncRunner runner) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "calendar-sync.sync", "Google 日曆同步", "日曆同步", "把公文的假單與出差單同步成共用日曆上的事件。", Schedule.Every(TimeSpan.FromMinutes(5)))
    {
        Unavailable = services => services.GetRequiredService<IOptions<CalendarSyncOptions>>().Value.Enabled ? null : "CalendarSync:Enabled 未啟用",
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var request = context.Trigger == ScheduledTaskTrigger.Manual
            ? new CalendarSyncRequest(context.RequestedBy ?? Guid.Empty, context.Parameters<CalendarSyncParameters>()?.ConfirmRemovals == true)
            : null;
        var outcome = await runner.RunAsync(request, ct);
        var summary = $"新增 {outcome.Created:N0}、更新 {outcome.Updated:N0}、移除 {outcome.Deleted:N0}、失敗 {outcome.Failed:N0}";
        return outcome.Status switch
        {
            SyncRunStatuses.Failed => TaskOutcome.Failed(CalendarSyncErrors.Failure(outcome.ErrorCode), summary),
            SyncRunStatuses.Held => TaskOutcome.Failed(CalendarSyncErrors.RemovalsHeld, $"{outcome.Held:N0} 個事件待移除，超過安全閥門檻"),
            _ when outcome.Created + outcome.Updated + outcome.Deleted + outcome.Failed == 0 => TaskOutcome.Idle(),
            _ => TaskOutcome.Done(summary),
        };
    }
}
