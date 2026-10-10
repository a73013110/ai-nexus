using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Chat;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Sharing;

/// <summary>Account-bound snapshots of conversations and artifacts. Deleting a source revokes its shares through domain events.</summary>
public sealed class SharingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ShareService>();
        builder.Services.AddScoped<ShareAccess>();
        builder.Services.AddSingleton<ShareWriteLock>();
        builder.Services.AddHostedService<ShareCleanupWorker>();
        builder.Services.AddFeaturePolicy(FeatureIds.Shared);
        builder.Services.AddDomainEventHandler<ConversationDeleted, RevokeDeletedSourceShares>();
        builder.Services.AddDomainEventHandler<ArtifactDeleted, RevokeDeletedSourceShares>();
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

internal sealed class SharingFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Shared, "分享", "/shared", 50));
