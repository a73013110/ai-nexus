using AiNexus.Data.App;
using AiNexus.Data.App.Scheduling;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>一輪執行紀錄；<see cref="RequestedByName"/> 是手動執行的管理員，其他為 null。<see cref="ErrorMessage"/> 只含固定提示與查證代碼。</summary>
public sealed record ScheduledTaskRunDto(
    Guid Id,
    string TaskKey,
    string TriggerKind,
    string? RequestedByName,
    string Host,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Status,
    string? Stage,
    int? CompletedUnits,
    int? TotalUnits,
    string? Summary,
    string? ErrorCode,
    string? ErrorMessage,
    string? IssueCode);

internal static class ScheduledTaskRunQueries
{
    public static IQueryable<ScheduledTaskRunDto> ToDtos(this IQueryable<ScheduledTaskRun> runs, AppDbContext db)
        => from r in runs
           join u in db.Users on r.RequestedById equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new ScheduledTaskRunDto(r.Id, r.TaskKey, r.TriggerKind, u.DisplayName, r.Host, r.StartedAt, r.FinishedAt, r.Status, r.Stage,
               r.CompletedUnits, r.TotalUnits, r.Summary, r.ErrorCode, r.ErrorMessage, r.IssueCode);
}
