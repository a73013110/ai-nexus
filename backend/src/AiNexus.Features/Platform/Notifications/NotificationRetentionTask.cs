using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>
/// 刪除建立超過 <see cref="NotificationsOptions.RetentionDays"/>、且已讀或已移除的通知。
/// 未讀的不刪：<see cref="NotificationExpiryTask"/> 會先把放了一週的未讀改為已讀，下一輪才輪到它，所以兩個任務誰先跑都不會刪掉還沒看過的通知。
/// 刪掉後同一個 <c>EventKey</c> 可以再發一次：一直沒解決的狀況（例如資料庫結構異動）每個保存期最多再提醒一次。
/// 這張表沒有以 <c>CreatedAt</c> 開頭的索引，每批掃一次表；一天一輪、分批刪除（<see cref="Batches"/>）。
/// </summary>
public sealed class NotificationRetentionTask(AppDbContext db, IOptions<NotificationsOptions> options, TimeProvider clock) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "notifications.retention", "通知清理", "通知", "刪除超過保存期、已讀或已移除的通知。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await DeleteExpiredAsync(ct), total => $"刪除 {total:N0} 則通知");

    /// <summary>分批刪除超過保存期的通知，回傳刪除的總筆數。</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        var expired = db.Notifications.Where(x => x.CreatedAt < before && (x.ReadAt != null || x.DismissedAt != null));
        return Batches.RepeatAsync(() => expired.Take(Batches.Size).ExecuteDeleteAsync(ct));
    }
}
