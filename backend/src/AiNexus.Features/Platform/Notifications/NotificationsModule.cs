using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Notifications;

/// <summary>工作區收件匣。其他模組透過 <see cref="NotificationService"/> 排入通知。</summary>
public sealed class NotificationsModule : IFeatureModule
{
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/notifications")
            .MapSlice<ListNotifications>()
            .MapSlice<ReadNotifications>()
            .MapSlice<DismissNotification>();
    }
}
