using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>手動同步帶給任務的參數（<see cref="Management.StartCalendarSyncRun"/>）；<see cref="Reset"/> 是重設日曆（<see cref="Management.ResetCalendarSync"/>），不同步。</summary>
public sealed record CalendarSyncParameters(bool ConfirmRemovals, bool Reset = false);

/// <summary>
/// 定期跑一輪 <see cref="CalendarSyncRunner"/>；管理頁的「立即同步」經排程任務的手動要求提前執行，並帶上是否確認移除。
/// <c>CalendarSync:Enabled</c> 為 false 時不執行（手動同步的端點也會拒絕）。無法完成（GD、憑證、權限、日曆）與安全閥擋下移除都算失敗：
/// 都需要人處理，連續失敗由排程通知管理員。
/// </summary>
public sealed class CalendarSyncTask(CalendarSyncRunner runner) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "calendar-sync.sync", "Google 日曆同步", "日曆同步", "把公文的假單與出差單同步成共用日曆上的事件。")
    {
        Unavailable = services => services.GetRequiredService<IOptions<CalendarSyncOptions>>().Value.Enabled ? null : "CalendarSync:Enabled 未啟用",
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var parameters = context.Trigger == ScheduledTaskTrigger.Manual ? context.Parameters<CalendarSyncParameters>() : null;
        var request = context.Trigger == ScheduledTaskTrigger.Manual
            ? new CalendarSyncRequest(context.RequestedBy ?? Guid.Empty, parameters?.ConfirmRemovals == true)
            : null;
        if (request is not null && parameters?.Reset == true)
        {
            var reset = await runner.ResetAsync(request, ct);
            return reset.Status == SyncRunStatuses.Succeeded
                ? TaskOutcome.Done($"重設日曆：移除 {reset.Deleted:N0} 個事件，清除事件與同步紀錄")
                : TaskOutcome.Failed(CalendarSyncErrors.Failure(reset.ErrorCode), $"重設未完成：已移除 {reset.Deleted:N0}、失敗 {reset.Failed:N0}");
        }
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
