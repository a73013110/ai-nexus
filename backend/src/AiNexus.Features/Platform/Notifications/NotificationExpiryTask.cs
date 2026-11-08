using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>
/// 未讀超過 <see cref="UnreadFor"/> 的通知改為已讀：放了一週還沒處理的事，徽章再亮也只是噪音；通知中心仍查得到。
/// 寫回資料而不是在查詢時換算，未讀的定義（<c>ReadAt</c> 為空）才只有一種，總覽的 procedure 不必跟著改。
/// 分批更新（<see cref="Batches"/>）。
/// </summary>
public sealed class NotificationExpiryTask(AppDbContext db, TimeProvider clock) : IScheduledTask
{
    public static readonly TimeSpan UnreadFor = TimeSpan.FromDays(7);

    public static ScheduledTaskDefinition Definition { get; } = new(
        "notifications.expire-unread", "過期通知標為已讀", "通知", "未讀超過 7 天的通知改為已讀，通知中心仍查得到。");

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => TaskOutcome.Count(await ExpireAsync(ct), total => $"{total:N0} 則改為已讀");

    /// <summary>把過期的未讀通知改為已讀，回傳筆數。</summary>
    public async Task<int> ExpireAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - UnreadFor;
        var expired = db.Notifications.Unread().Where(x => x.CreatedAt < cutoff);
        return await Batches.RepeatAsync(() => expired.Take(Batches.Size).ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, now), ct));
    }
}
