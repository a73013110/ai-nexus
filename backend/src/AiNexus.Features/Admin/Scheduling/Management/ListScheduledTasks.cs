using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>
/// 一個任務的看板資料。<see cref="UnavailableReason"/> 非空時不能執行；<see cref="CurrentRun"/> 只在租約有效時才有（程序中止留下的 running 紀錄不算）；
/// <see cref="RecentRuns"/> 是最近 <see cref="ListScheduledTasks.RecentRunCount"/> 輪（新到舊，含執行中），前端畫成紀錄條。
/// </summary>
public sealed record ScheduledTaskDto(
    string Key,
    string Name,
    string Category,
    string Description,
    string Schedule,
    string ScheduleKind,
    int? IntervalSeconds,
    int TimeoutSeconds,
    string? UnavailableReason,
    bool IsPaused,
    string? PausedByName,
    DateTimeOffset? PausedAt,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? RunRequestedAt,
    bool CancelRequested,
    string? Host,
    int ConsecutiveFailures,
    ScheduledTaskRunDto? CurrentRun,
    ScheduledTaskRunDto? LastRun,
    IReadOnlyList<ScheduledTaskRunDto> RecentRuns);

/// <summary>
/// <see cref="ServerTime"/> 讓前端以伺服器時間倒數，不受使用者電腦時鐘誤差影響；
/// <see cref="ExecutesHere"/> 為 false 表示回應這個請求的程序不執行排程（<c>Scheduling:Execute</c>），由其他程序執行。
/// </summary>
public sealed record ScheduledTaskBoardDto(DateTimeOffset ServerTime, bool ExecutesHere, IReadOnlyList<ScheduledTaskDto> Tasks);

/// <summary>
/// 所有排程任務的狀態，前端每幾秒輪詢一次：任務列每個任務一列，紀錄走 <c>ix_ScheduledTaskRuns_TaskKey_StartedAt</c> 各取最近幾輪，
/// 不隨紀錄量變慢。只有營運資料（任務、時間、結果摘要），不含個人資料，所以不稽核。
/// </summary>
internal sealed class ListScheduledTasks(AppDbContext db, ScheduledTaskCatalog catalog, IServiceProvider services, IOptions<SchedulingOptions> options, TimeProvider clock) : IEndpointSlice
{
    public const int RecentRunCount = 24;

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ListScheduledTasks handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("排程任務的營運狀態，不含個人資料；頁面會輪詢")
        .WithName("ListScheduledTasks");

    public async Task<ScheduledTaskBoardDto> HandleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var keys = catalog.All.Select(x => x.Key).ToList();
        var rows = await (from t in db.ScheduledTasks.AsNoTracking()
                          where keys.Contains(t.Key)
                          join u in db.Users on t.PausedById equals u.Id into pausers
                          from u in pausers.DefaultIfEmpty()
                          select new { Task = t, PausedByName = (string?)u.DisplayName })
            .ToDictionaryAsync(x => x.Task.Key, StringComparer.Ordinal, ct);
        // 以任務列 CROSS APPLY 各取最近幾輪：每個任務走一次 ix_ScheduledTaskRuns_TaskKey_StartedAt 的倒序掃描。
        var runs = (await db.ScheduledTasks.AsNoTracking()
                .Where(t => keys.Contains(t.Key))
                .SelectMany(t => db.ScheduledTaskRuns.Where(r => r.TaskKey == t.Key).OrderByDescending(r => r.StartedAt).Take(RecentRunCount))
                .ToDtos(db)
                .ToListAsync(ct))
            .GroupBy(x => x.TaskKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScheduledTaskRunDto>)[.. g.OrderByDescending(x => x.StartedAt)], StringComparer.Ordinal);

        var tasks = catalog.All.Select(plan =>
        {
            var row = rows.GetValueOrDefault(plan.Key);
            var task = row?.Task;
            var recent = runs.GetValueOrDefault(plan.Key) ?? [];
            var leased = task?.LeaseUntil > now;
            var current = leased ? recent.FirstOrDefault(x => x.Status == ScheduledTaskRunStatuses.Running) : null;
            return new ScheduledTaskDto(
                plan.Key, plan.Definition.Name, plan.Definition.Category, plan.Definition.Description,
                plan.Schedule.Description, plan.Schedule.Kind, (int?)plan.Schedule.Interval?.TotalSeconds, (int)plan.Timeout.TotalSeconds,
                plan.UnavailableReason(services),
                task?.IsPaused ?? false, row?.PausedByName, task?.PausedAt,
                // 還沒有任何程序執行過（沒有任務列）時，顯示依排程推算的第一次時間。
                task is null ? plan.Schedule.First(now) : task.NextRunAt,
                task?.RunRequestedAt, task?.CancelRequested ?? false, leased ? task?.LeaseHost : null, task?.ConsecutiveFailures ?? 0,
                current, recent.FirstOrDefault(x => x.Status != ScheduledTaskRunStatuses.Running), recent);
        }).ToList();
        return new(now, options.Value.Execute, tasks);
    }
}
