using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Flow.Maintenance;

/// <summary>
/// 關卡等待超過 <see cref="FlowOptions.RemindAfterDays"/> 天時提醒目前的審核人（指派快取上的人）。通知鍵含日期，
/// 同一天重跑不重送；只寫通知與郵件，不動申請，所以一次提交即可。
/// </summary>
public sealed class FlowReminderTask(AppDbContext db, FlowEngine engine, FlowNotices notices, IOptions<FlowOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "flow.reminders", "簽核提醒", "簽核", "關卡等待超過設定天數時，每天提醒一次目前的審核人。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.RemindAfterDays);
        var waiting = await db.FlowInstanceSteps.AsNoTracking()
            .Where(s => s.Status == FlowStepStatuses.Active && s.ActivatedAt < cutoff)
            .Select(s => s.InstanceId).ToListAsync(ct);
        var reminded = 0;
        foreach (var id in waiting)
        {
            if (await engine.LoadAsync(id, ct) is not { Current: { } step } flow) continue;
            var users = (await db.FlowAssignments.AsNoTracking().Where(x => x.InstanceId == id).Select(x => x.UserId).ToListAsync(ct)).ToHashSet();
            await notices.RemindAsync(flow, step, users, now, ct);
            reminded++;
        }
        await db.SaveChangesAsync(ct);
        return TaskOutcome.Count(reminded, total => $"提醒 {total:N0} 筆申請的審核人");
    }
}
