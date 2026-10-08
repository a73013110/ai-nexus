using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Sharing;

/// <summary>Account-bound snapshots of conversations and artifacts. Other modules revoke shares through <see cref="ShareService"/>.</summary>
public sealed class SharingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ShareService>();
        builder.Services.AddScoped<ShareAccess>();
        builder.Services.AddScoped<CreateShare>();
        builder.Services.AddScoped<OpenSharedFile>();
        builder.Services.AddSingleton<ShareWriteLock>();
        builder.Services.AddHostedService<ShareCleanupWorker>();
        builder.Services.AddFeaturePolicy(FeatureIds.Shared);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/shares").RequireAuthorization(Policies.Shared).WithTags("Sharing");
        ListShares.Map(routes);
        CreateShare.Map(routes);
        ReadShare.Map(routes);
        RevokeShare.Map(routes);
        OpenSharedFile.Map(routes);
    }
}
