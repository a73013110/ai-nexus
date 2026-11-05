using AiNexus.Data.App.Notifications;

namespace AiNexus.Features.Platform.Notifications;

internal static class NotificationQueries
{
    /// <summary>擁有者仍在收件匣中的通知。</summary>
    public static IQueryable<Notification> Inbox(this IQueryable<Notification> notifications, Guid owner)
        => notifications.Where(x => x.OwnerId == owner && x.DismissedAt == null);

    /// <summary>仍在收件匣且未讀的通知（任何擁有者）。</summary>
    public static IQueryable<Notification> Unread(this IQueryable<Notification> notifications)
        => notifications.Where(x => x.DismissedAt == null && x.ReadAt == null);

    /// <summary>指向同一個目標的通知。</summary>
    public static IQueryable<Notification> About(this IQueryable<Notification> notifications, string kind, Guid id)
        => notifications.Where(x => x.TargetKind == kind && x.TargetId == id);
}
