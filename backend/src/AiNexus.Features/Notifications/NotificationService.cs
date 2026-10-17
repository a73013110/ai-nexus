using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

/// <summary>其他模組排入通知的契約入口。</summary>
public sealed class NotificationService(NexusDbContext db)
{
    /// <summary>必須與來源變更在同一個交易中由呼叫端儲存；同一 EventKey 重複排入會被忽略。</summary>
    public async Task PublishAsync(Guid owner, string key, string type, string severity, string title, string body, string target, Guid id, CancellationToken ct, string? issueCode = null)
    {
        if (target is not ("conversation" or "task" or "share" or "repository-review") || severity is not ("info" or "success" or "error"))
            throw new InvalidOperationException("Unknown notification target or severity.");
        if (db.Set<WorkspaceNotification>().Local.Any(x => x.OwnerId == owner && x.EventKey == key)
            || await db.Set<WorkspaceNotification>().AnyAsync(x => x.OwnerId == owner && x.EventKey == key, ct)) return;
        db.Add(new WorkspaceNotification { OwnerId = owner, EventKey = key, Type = type, Severity = severity,
            Title = severity == "error" ? "操作未完成" : string.Concat(title.Take(180)), Body = severity == "error" ? Issues.Message(issueCode) : string.Concat(body.Take(600)), IssueCode = issueCode, TargetKind = target, TargetId = id });
    }
}
