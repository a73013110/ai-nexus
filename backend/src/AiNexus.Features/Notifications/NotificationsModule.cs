using AiNexus.Platform.Modules;

namespace AiNexus.Features.Notifications;

public sealed class NotificationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder) => builder.Services.AddScoped<NotificationService>();

    public static void MapEndpoints(RouteGroupBuilder api) => NotificationEndpoints.MapNotifications(api);
}
