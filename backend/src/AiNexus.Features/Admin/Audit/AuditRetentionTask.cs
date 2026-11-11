using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Audit;

/// <summary>
/// 稽核的保存期清理：刪除 <c>audit.AuditEvents</c> 裡超過 <see cref="AuditOptions.RetentionDays"/> 的紀錄。
/// 保存期與系統日誌分開設定：稽核是安全紀錄，保存得比診斷用的日誌久。
/// 由舊到新分批刪除（<see cref="Batches"/>，走 <c>IX_AuditEvents_At</c>）。
/// </summary>
public sealed class AuditRetentionTask(AppDbContext db, IOptions<AuditOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "audit.retention", "稽核紀錄清理", "活動稽核", "刪除超過保存期的稽核紀錄。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await DeleteExpiredAsync(ct), total => $"刪除 {total:N0} 筆稽核");

    /// <summary>分批刪除超過保存期的稽核，回傳刪除的總筆數。</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return db.AuditEvents.Where(x => x.At < before).OrderBy(x => x.At).DeleteInBatchesAsync(ct);
    }
}
