using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Flow.Maintenance;

/// <summary>
/// 審核中的申請逐筆處理，每筆自己的交易：超過 <see cref="FlowOptions.ExpireAfterDays"/> 的改為逾期，其餘依目前的組織與授權重算指派
/// （指派是快取，ADR 0036）。與使用者的決定撞上的那筆留到下一輪，不算任務失敗。
/// </summary>
public sealed class FlowMaintenanceTask(AppDbContext db, FlowEngine engine, IOptions<FlowOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "flow.maintenance", "簽核指派與逾期", "簽核", "依目前的組織與授權重算待審關卡的審核人，並把超過期限的申請改為逾期。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.ExpireAfterDays);
        var pending = await db.FlowInstances.AsNoTracking().Where(x => x.Status == FlowStatuses.Pending)
            .OrderBy(x => x.SubmittedAt).Select(x => new { x.Id, x.SubmittedAt }).ToListAsync(ct);
        int expired = 0, reassigned = 0, deferred = 0;
        foreach (var item in pending)
        {
            var changes = 0;
            var result = item.SubmittedAt < cutoff
                ? await engine.ChangeAsync(item.Id, flow => engine.CloseAsync(flow, FlowStatuses.Expired, null, ct), ct)
                : await engine.ChangeAsync(item.Id, async flow => { changes = await engine.RefreshAsync(flow, ct); return Result.Success; }, ct);
            // 每筆各自提交；清掉已提交的追蹤，長清單不會累積在同一個 context。
            db.ChangeTracker.Clear();
            if (!result.IsSuccess) deferred++;
            else if (item.SubmittedAt < cutoff) expired++;
            else if (changes > 0) reassigned++;
        }
        return expired + reassigned + deferred == 0 ? TaskOutcome.Idle()
            : TaskOutcome.Done($"{expired:N0} 筆逾期、{reassigned:N0} 筆重新指派" + (deferred > 0 ? $"、{deferred:N0} 筆留待下一輪" : ""));
    }
}
