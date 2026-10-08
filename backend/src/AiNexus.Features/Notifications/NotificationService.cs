using AiNexus.Features.Persistence;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Notifications;

/// <summary>The module's contract for other modules: queue a notification alongside their own change.</summary>
public sealed class NotificationService(NexusDbContext db)
{
    /// <summary>Caller saves the event with its source change in the same transaction.</summary>
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
