using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Jobs.Retention;

/// <summary>
/// 刪除結束超過 <see cref="JobsOptions.RetentionDays"/> 的佇列工作。排隊中與執行中的不刪；
/// 還有資料指著的也不刪（評測結果、程式碼 Review、文件目前的處理工作…，由各模組以 <see cref="IJobReferences"/> 宣告），那些工作跟著引用它的資料走。
/// 保存期從最後一次更新起算，重試過的工作不會因為建立得早就被刪；另外帶上建立時間的條件，才走得到 <c>ix_BackgroundJobs_CreatedAt</c>。
/// 分批刪除（<see cref="Batches"/>）。
/// </summary>
public sealed class JobRetentionTask(AppDbContext db, IEnumerable<IJobReferences> references, IOptions<JobsOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "jobs.retention", "佇列工作清理", "背景工作", "刪除結束超過保存期、且沒有其他資料引用的佇列工作。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await DeleteExpiredAsync(ct), total => $"刪除 {total:N0} 件工作");

    /// <summary>分批刪除超過保存期且沒被引用的工作，回傳刪除的總筆數。</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        var expired = db.BackgroundJobs.Where(x => x.CreatedAt < before && x.UpdatedAt < before
            && x.Status != RunStates.Queued && x.Status != RunStates.Running);
        foreach (var source in references)
        {
            var referenced = source.JobIds(db);
            expired = expired.Where(x => !referenced.Contains(x.Id));
        }
        return expired.OrderBy(x => x.CreatedAt).DeleteInBatchesAsync(ct);
    }
}
