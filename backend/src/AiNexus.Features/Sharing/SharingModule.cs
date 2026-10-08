using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Sharing;

public sealed class SharingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ShareService>();
        builder.Services.AddSingleton<ShareWriteLock>();
        builder.Services.AddHostedService<ShareCleanupWorker>();
        builder.Services.AddFeaturePolicy("shared");
    }

    public static void MapEndpoints(RouteGroupBuilder api) => ShareEndpoints.MapSharing(api);
}
