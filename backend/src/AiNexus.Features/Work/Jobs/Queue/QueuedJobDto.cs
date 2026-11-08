using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Data.App.Jobs;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Work.Jobs.Queue;

/// <summary>
/// 佇列頁的一筆工作。狀態 running 但 <see cref="LeaseUntil"/> 已過期，是執行的程序中止、等待接手；<see cref="ErrorMessage"/> 只含固定提示與查證代碼。
/// </summary>
public sealed record QueuedJobDto(
    Guid Id,
    string Kind,
    string Label,
    string OwnerName,
    string Status,
    string Stage,
    int Attempt,
    int CompletedUnits,
    int? TotalUnits,
    bool CancelRequested,
    DateTimeOffset? LeaseUntil,
    string? ErrorCode,
    string? ErrorMessage,
    string? IssueCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal static class QueuedJobQueries
{
    // 錯誤提示在最後一層投影於用戶端組成（EF Core 允許），與使用者自己的背景任務相同。
    public static IQueryable<QueuedJobDto> ToQueuedDtos(this IQueryable<BackgroundJob> jobs, AppDbContext db)
        => from j in jobs
           join u in db.Users on j.OwnerId equals u.Id
           select new QueuedJobDto(j.Id, j.Kind, j.Label, u.DisplayName, j.Status, j.Stage, j.Attempt, j.CompletedUnits, j.TotalUnits,
               j.CancelRequested, j.LeaseUntil, j.ErrorCode, j.Status == RunStates.Failed ? j.ErrorMessage ?? Issues.Message(j.IssueCode) : null, j.IssueCode,
               j.CreatedAt, j.UpdatedAt);
}
