using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>
/// 刪除超過 <see cref="CalendarSyncOptions.RunRetentionDays"/> 的同步紀錄。依開始時間分批刪除（<see cref="Batches"/>，走 <c>ix_SyncRuns_StartedAt</c>）。
/// 不看 <c>CalendarSync:Enabled</c>：關閉同步後舊紀錄仍要照保存期清掉。
/// </summary>
internal sealed class SyncRunRetentionTask(AppDbContext db, IOptions<CalendarSyncOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "calendar-sync.run-retention", "同步紀錄清理", "日曆同步", "刪除超過保存期的日曆同步紀錄。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.RunRetentionDays);
        var expired = db.SyncRuns.Where(x => x.StartedAt < cutoff).OrderBy(x => x.StartedAt);
        var total = await Batches.RepeatAsync(() => expired.Take(Batches.Size).ExecuteDeleteAsync(ct));
        return TaskOutcome.Count(total, count => $"刪除 {count:N0} 筆紀錄");
    }
}
