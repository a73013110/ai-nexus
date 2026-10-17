using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Notifications;

/// <summary>工作區收件匣。其他模組透過 <see cref="NotificationService"/> 排入通知。</summary>
public sealed class NotificationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddDashboardSource<NotificationsDashboard>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/notifications").WithTags("Notifications");
        ListNotifications.Map(routes);
        ReadNotifications.Map(routes);
        DismissNotification.Map(routes);
    }
}
