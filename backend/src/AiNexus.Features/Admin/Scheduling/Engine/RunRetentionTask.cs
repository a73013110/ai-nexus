using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>
/// 清理排程任務自己的執行紀錄：idle 保存 <see cref="SchedulingOptions.IdleRunRetentionDays"/>，其他保存 <see cref="SchedulingOptions.RunRetentionDays"/>。
/// 依開始時間分批刪除（<see cref="Batches"/>，走 <c>ix_ScheduledTaskRuns_StartedAt</c>）。
/// </summary>
internal sealed class RunRetentionTask(AppDbContext db, IOptions<SchedulingOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "scheduling.run-retention", "執行紀錄清理", "背景作業", "刪除超過保存期的排程執行紀錄；沒事可做的紀錄保存期較短。", Schedule.Daily(3, 30));

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var idleBefore = now.AddDays(-options.Value.IdleRunRetentionDays);
        var before = now.AddDays(-options.Value.RunRetentionDays);
        var idle = await DeleteAsync(db.ScheduledTaskRuns.Where(x => x.StartedAt < idleBefore && x.Status == ScheduledTaskRunStatuses.Idle), ct);
        var others = await DeleteAsync(db.ScheduledTaskRuns.Where(x => x.StartedAt < before && x.Status != ScheduledTaskRunStatuses.Running), ct);
        return TaskOutcome.Count(idle + others, total => $"刪除 {total:N0} 筆紀錄");
    }

    private static Task<int> DeleteAsync(IQueryable<ScheduledTaskRun> expired, CancellationToken ct)
        => Batches.RepeatAsync(() => expired.OrderBy(x => x.StartedAt).Take(Batches.Size).ExecuteDeleteAsync(ct));
}
