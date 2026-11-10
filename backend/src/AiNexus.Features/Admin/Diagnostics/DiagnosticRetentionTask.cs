using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>
/// 系統日誌的保存期清理：刪除 <c>diagnostics.DiagnosticEvents</c> 裡超過 <see cref="DiagnosticOptions.RetentionDays"/> 的事件。
/// 只管資料庫；本機日誌檔由各程序的 <see cref="DiagnosticWorker"/> 自己清（檔案在各自的主機上）。
/// 由舊到新分批刪除（<see cref="Batches"/>，走叢集索引 <c>IX_DiagnosticEvents_At_LogId</c>）。
/// </summary>
public sealed class DiagnosticRetentionTask(AppDbContext db, IOptions<DiagnosticOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "diagnostics.retention", "系統日誌清理", "系統日誌", "刪除超過保存期的系統日誌；本機日誌檔由各站台自行清理。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await DeleteExpiredAsync(ct), total => $"刪除 {total:N0} 筆日誌");

    /// <summary>分批刪除超過保存期的事件，回傳刪除的總筆數。</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return Batches.RepeatAsync(() => db.DiagnosticEvents.Where(x => x.At < before).OrderBy(x => x.At).Take(Batches.Size).ExecuteDeleteAsync(ct));
    }
}
