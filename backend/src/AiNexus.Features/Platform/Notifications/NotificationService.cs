using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>其他模組排入通知的契約入口。</summary>
public sealed class NotificationService(AppDbContext db)
{
    /// <summary>必須與來源變更在同一個交易中由呼叫端儲存；同一 EventKey 重複排入會被忽略。</summary>
    public async Task PublishAsync(Guid owner, string key, string type, string severity, string title, string body, string target, Guid id, CancellationToken ct, string? issueCode = null)
    {
        if (!NotificationDisplay.IsTarget(target) || severity is not ("info" or "success" or NotificationDisplay.Error))
            throw new InvalidOperationException("Unknown notification target or severity.");
        // 同一交易裡尚未儲存的通知只存在 Local，資料庫查不到，所以兩邊都要檢查，否則同一交易內的重複會撞上唯一索引。
        if (db.Set<Notification>().Local.Any(x => x.OwnerId == owner && x.EventKey == key)
            || await db.Set<Notification>().AnyAsync(x => x.OwnerId == owner && x.EventKey == key, ct)) return;
        db.Add(new Notification { OwnerId = owner, EventKey = key, Type = type, Severity = severity,
            Title = NotificationDisplay.Title(severity, string.Concat(title.Take(180))), Body = NotificationDisplay.Body(severity, string.Concat(body.Take(600)), issueCode), IssueCode = issueCode, TargetKind = target, TargetId = id });
    }
}
