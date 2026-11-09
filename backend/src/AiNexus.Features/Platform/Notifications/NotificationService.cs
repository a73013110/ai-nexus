using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Strings;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>要排入的一則通知；<see cref="Key"/> 在同一收件者內唯一，重送會被忽略。</summary>
public sealed record NotificationMessage(
    Guid Owner, string Key, string Type, string Severity, string Title, string Body, string TargetKind, Guid TargetId)
{
    public string? IssueCode { get; init; }

    /// <summary>
    /// 收件者自己發起的事的結果（送出提問、啟動任務）。這類結果只有失敗需要處理，
    /// 成功或取消排入時就是已讀：留在通知中心備查，不計入未讀與徽章。
    /// </summary>
    public bool OwnAction { get; init; }

    internal bool NeedsAttention => !OwnAction || Severity == NotificationDisplay.Error;
}

/// <summary>其他模組排入與撤下通知的契約入口。未讀只留給需要收件者處理的事，規則集中在這裡。</summary>
[Scoped]
public sealed class NotificationService(AppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// 必須與來源變更在同一個交易中由呼叫端儲存。同一目標較早的未讀通知會改為已讀：
    /// 新結果取代舊結果（例：重試成功後，前一次的失敗不必再手動點掉）。
    /// </summary>
    public async Task PublishAsync(NotificationMessage message, CancellationToken ct)
    {
        var (owner, key) = (message.Owner, message.Key);
        if (!NotificationDisplay.IsTarget(message.TargetKind) || message.Severity is not ("info" or "success" or NotificationDisplay.Error))
            throw new InvalidOperationException("Unknown notification target or severity.");
        // 同一交易裡尚未儲存的通知只存在 Local，資料庫查不到，所以兩邊都要檢查，否則同一交易內的重複會撞上唯一索引。
        if (db.Notifications.Local.Any(x => x.OwnerId == owner && x.EventKey == key)
            || await db.Notifications.AnyAsync(x => x.OwnerId == owner && x.EventKey == key, ct)) return;

        var now = clock.GetUtcNow();
        // 載入後由呼叫端一起儲存，取代與新通知同生共死，不會只成功一半。
        foreach (var older in await db.Notifications.Unread().About(message.TargetKind, message.TargetId)
                     .Where(x => x.OwnerId == owner).ToListAsync(ct))
            older.ReadAt = now;

        var severity = message.Severity;
        db.Add(new Notification
        {
            OwnerId = owner, EventKey = key, Type = message.Type, Severity = severity,
            Title = NotificationDisplay.Title(severity, message.Title.Clip(180)),
            Body = NotificationDisplay.Body(severity, message.Body.Clip(600), message.IssueCode),
            IssueCode = message.IssueCode, TargetKind = message.TargetKind, TargetId = message.TargetId,
            ReadAt = message.NeedsAttention ? null : now,
        });
    }

    /// <summary>目標已恢復正常（例：結構異動已處理），所有人關於它的未讀通知改為已讀；立即生效，不需另外儲存。</summary>
    public Task ResolveAsync(string targetKind, Guid targetId, CancellationToken ct)
        => db.Notifications.Unread().About(targetKind, targetId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct);
}
