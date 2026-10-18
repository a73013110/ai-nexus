using AiNexus.Data.App.Notifications;

namespace AiNexus.Features.Notifications;

internal static class NotificationQueries
{
    /// <summary>擁有者仍在收件匣中的通知。</summary>
    public static IQueryable<Notification> Inbox(this IQueryable<Notification> notifications, Guid owner)
        => notifications.Where(x => x.OwnerId == owner && x.DismissedAt == null);
}
