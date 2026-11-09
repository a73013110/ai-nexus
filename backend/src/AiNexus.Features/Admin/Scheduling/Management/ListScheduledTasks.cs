using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>
/// 一個任務的看板資料。<see cref="UnavailableReason"/> 非空時不能執行；<see cref="CurrentRun"/> 只在租約有效時才有（程序中止留下的 running 紀錄不算）；
/// <see cref="TimelineRuns"/> 是時間軸區間內已開始的輪次（舊到新，含執行中）；
/// <see cref="PlannedRuns"/> 是區間內 cron 排程固定的時刻，固定間隔的任務由前端以 <see cref="NextRunAt"/> 與 <see cref="IntervalSeconds"/> 推算，不佔輪詢回應。
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
    IReadOnlyList<ScheduledTaskTimelineRunDto> TimelineRuns,
    IReadOnlyList<DateTimeOffset> PlannedRuns);

/// <summary>時間軸上的一輪：只有畫記號與提示要用的欄位，輪詢的回應才不會隨輪數變肥。</summary>
public sealed record ScheduledTaskTimelineRunDto(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, string? Summary);

/// <summary>
/// <see cref="ServerTime"/> 讓前端以伺服器時間倒數，不受使用者電腦時鐘誤差影響；
/// <see cref="ExecutesHere"/> 為 false 表示回應這個請求的程序不執行排程，或只執行部分任務（<c>Scheduling:Execute</c>、<c>Scheduling:ExecuteOnly</c>），其餘由其他程序執行；
/// <see cref="TimelineFrom"/>、<see cref="TimelineTo"/> 是時間軸的區間，由伺服器決定，前端與資料的範圍才不會各算各的。
/// </summary>
public sealed record ScheduledTaskBoardDto(
    DateTimeOffset ServerTime,
    bool ExecutesHere,
    DateTimeOffset TimelineFrom,
    DateTimeOffset TimelineTo,
    IReadOnlyList<ScheduledTaskDto> Tasks);

/// <summary>
/// 所有排程任務的狀態，前端每幾秒輪詢一次：任務列每個任務一列，紀錄走 <c>ix_ScheduledTaskRuns_TaskKey_StartedAt</c> 依區間取，
/// 不隨紀錄量變慢。只有營運資料（任務、時間、結果摘要），不含個人資料，所以不稽核。
/// </summary>
internal sealed class ListScheduledTasks(AppDbContext db, ScheduledTaskCatalog catalog, IServiceProvider services, TimeProvider clock) : IEndpointSlice
{
    // 同一任務最多一筆 running（新一輪開始時會把舊的標成中斷），所以最新兩筆一定含「執行中」與「上一輪」。
    private const int LatestRunCount = 2;
    private const int MaxTimelineRuns = 300;
    private const int MaxPlannedRuns = 288;
    private static readonly TimeSpan TimelinePast = TimeSpan.FromHours(4);
    private static readonly TimeSpan TimelineSpan = TimeSpan.FromHours(24);

    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ListScheduledTasks handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("排程任務的營運狀態，不含個人資料；頁面會輪詢")
        .WithName("ListScheduledTasks");

    public async Task<ScheduledTaskBoardDto> HandleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // 區間對齊整點：一小時才移動一次，輪詢之間刻度與記號都不必重排。
        var timelineFrom = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerHour)) - TimelinePast;
        var timelineTo = timelineFrom + TimelineSpan;
        var keys = catalog.All.Select(x => x.Key).ToList();
        var rows = await (from t in db.ScheduledTasks.AsNoTracking()
                          where keys.Contains(t.Key)
                          join u in db.Users on t.PausedById equals u.Id into pausers
                          from u in pausers.DefaultIfEmpty()
                          select new { Task = t, PausedByName = (string?)u.DisplayName })
            .ToDictionaryAsync(x => x.Task.Key, StringComparer.Ordinal, ct);
        // 以任務列 CROSS APPLY 各取幾輪：每個任務走一次 ix_ScheduledTaskRuns_TaskKey_StartedAt 的倒序掃描。
        var latest = (await db.ScheduledTasks.AsNoTracking()
                .Where(t => keys.Contains(t.Key))
                .SelectMany(t => db.ScheduledTaskRuns.Where(r => r.TaskKey == t.Key).OrderByDescending(r => r.StartedAt).Take(LatestRunCount))
                .ToDtos(db)
                .ToListAsync(ct))
            .GroupBy(x => x.TaskKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScheduledTaskRunDto>)[.. g.OrderByDescending(x => x.StartedAt)], StringComparer.Ordinal);
        var timeline = (await db.ScheduledTasks.AsNoTracking()
                .Where(t => keys.Contains(t.Key))
                .SelectMany(t => db.ScheduledTaskRuns.Where(r => r.TaskKey == t.Key && r.StartedAt >= timelineFrom).OrderByDescending(r => r.StartedAt).Take(MaxTimelineRuns))
                .Select(r => new { r.TaskKey, Run = new ScheduledTaskTimelineRunDto(r.StartedAt, r.FinishedAt, r.Status, r.Summary) })
                .ToListAsync(ct))
            .GroupBy(x => x.TaskKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScheduledTaskTimelineRunDto>)[.. g.Select(x => x.Run).OrderBy(x => x.StartedAt)], StringComparer.Ordinal);

        var tasks = catalog.All.Select(plan =>
        {
            var row = rows.GetValueOrDefault(plan.Key);
            var task = row?.Task;
            var recent = latest.GetValueOrDefault(plan.Key) ?? [];
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
                current, recent.FirstOrDefault(x => x.Status != ScheduledTaskRunStatuses.Running),
                timeline.GetValueOrDefault(plan.Key) ?? [],
                [.. plan.Schedule.Occurrences(now, timelineTo).Take(MaxPlannedRuns)]);
        }).ToList();
        return new(now, catalog.All.All(x => x.ExecutesHere), timelineFrom, timelineTo, tasks);
    }
}
