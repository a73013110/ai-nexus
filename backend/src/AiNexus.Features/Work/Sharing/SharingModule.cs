using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Work.Chat.Transcripts;
using AiNexus.Features.Work.Sharing.Management;
using AiNexus.Features.Work.Sharing.Viewing;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Sharing;

/// <summary>綁定帳號的對話與成果快照。刪除來源時，透過領域事件撤銷其分享。</summary>
public sealed class SharingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddDashboardSource<SharingDashboard>();
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
