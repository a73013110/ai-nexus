using AiNexus.Platform.Modules;

namespace AiNexus.Features.Notifications;

/// <summary>Workspace inbox. Other modules publish through <see cref="NotificationService"/>; each use case has its own file.</summary>
public sealed class NotificationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<NotificationService>();

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/notifications").WithTags("Notifications");
        ListNotifications.Map(routes);
        ReadNotifications.Map(routes);
        DismissNotification.Map(routes);
    }
}
