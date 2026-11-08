using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>
/// 一個任務目前的狀態，給擁有任務的模組在自己的頁面或監控顯示（例如日曆同步的「最後檢查」）。
/// <see cref="Interval"/> 是套用設定覆寫後的間隔，cron 與啟動任務為 null；<see cref="LastRun"/> 是最近一輪已結束的紀錄。
/// </summary>
public sealed record ScheduledTaskState(bool Running, bool Requested, DateTimeOffset? NextRunAt, TimeSpan? Interval, ScheduledTaskRun? LastRun);

/// <summary>其他模組使用排程任務的入口：要求執行一輪、讀取狀態。管理頁的操作也經過這裡。</summary>
[Scoped]
public sealed class ScheduledTasks(AppDbContext db, ScheduledTaskCatalog catalog, ScheduledTaskSignal signal, IServiceProvider services, TimeProvider clock)
{
    /// <summary>
    /// 要求盡快執行一輪；正在執行時排在這一輪之後。<paramref name="parameters"/> 由任務以 <c>ScheduledTaskContext.Parameters&lt;T&gt;</c> 讀回。
    /// 連續要求只會執行一次，參數以最後一次為準。交易由這裡提交。
    /// </summary>
    public async Task<Result> RequestRunAsync(string key, Guid? actor, object? parameters, CancellationToken ct)
    {
        var plan = catalog.Find(key);
        if (plan is null) return SchedulingErrors.TaskNotFound;
        if (plan.UnavailableReason(services) is not null) return SchedulingErrors.TaskUnavailable;
        var json = parameters is null ? null : JsonSerializer.Serialize(parameters, JsonSerializerOptions.Web);
        if (json?.Length > ScheduledTask.ParametersMaxLength)
            throw new ArgumentException($"Parameters for '{key}' exceed {ScheduledTask.ParametersMaxLength} characters.", nameof(parameters));

        var now = clock.GetUtcNow();
        var task = await RowAsync(plan, now, ct);
        task.RunRequestedAt = now;
        task.RunRequestedById = actor;
        task.RunParameters = json;
        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        signal.Wake();
        return Result.Success;
    }

    /// <summary>要求停止執行中的這一輪，並撤回還沒開始的手動要求；沒有在執行時什麼也不做。</summary>
    public async Task<Result> CancelAsync(string key, CancellationToken ct)
    {
        if (catalog.Find(key) is null) return SchedulingErrors.TaskNotFound;
        var now = clock.GetUtcNow();
        await db.ScheduledTasks.Where(x => x.Key == key)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.CancelRequested, x => x.LeaseToken != null)
                .SetProperty(x => x.RunRequestedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.RunRequestedById, (Guid?)null)
                .SetProperty(x => x.RunParameters, (string?)null)
                .SetProperty(x => x.UpdatedAt, now), ct);
        signal.CancelLocal(key);
        return Result.Success;
    }

    /// <summary>暫停或恢復依排程執行；手動執行不受影響。恢復時已過的排程立即補跑一次。</summary>
    public async Task<Result> SetPausedAsync(string key, bool paused, Guid actor, CancellationToken ct)
    {
        var plan = catalog.Find(key);
        if (plan is null) return SchedulingErrors.TaskNotFound;
        var now = clock.GetUtcNow();
        var task = await RowAsync(plan, now, ct);
        if (task.IsPaused == paused) return Result.Success;
        task.IsPaused = paused;
        task.PausedAt = paused ? now : null;
        task.PausedById = paused ? actor : null;
        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        if (!paused) signal.Wake();
        return Result.Success;
    }

    public async Task<ScheduledTaskState?> GetStateAsync(string key, CancellationToken ct)
    {
        var plan = catalog.Find(key);
        if (plan is null) return null;
        var now = clock.GetUtcNow();
        var task = await db.ScheduledTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        var last = await db.ScheduledTaskRuns.AsNoTracking()
            .Where(x => x.TaskKey == key && x.Status != ScheduledTaskRunStatuses.Running)
            .OrderByDescending(x => x.StartedAt).FirstOrDefaultAsync(ct);
        return new(task?.LeaseUntil > now, task?.RunRequestedAt is not null, task?.NextRunAt, plan.Schedule.Interval, last);
    }

    /// <summary>還沒有任何程序執行過排程時，任務列可能還不存在；這裡補上，排程時間依任務的第一次排程。</summary>
    private async Task<ScheduledTask> RowAsync(ScheduledTaskPlan plan, DateTimeOffset now, CancellationToken ct)
    {
        var task = await db.ScheduledTasks.SingleOrDefaultAsync(x => x.Key == plan.Key, ct);
        if (task is not null) return task;
        task = new ScheduledTask { Key = plan.Key, NextRunAt = plan.Schedule.First(now), UpdatedAt = now };
        db.Add(task);
        return task;
    }
}
