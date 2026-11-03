using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Sharing.Management;
using AiNexus.Features.Work.Sharing.Viewing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Sharing;

/// <summary>綁定帳號的對話與成果快照。刪除來源時，透過領域事件撤銷其分享。</summary>
public sealed class SharingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ShareService>();
        builder.Services.AddScoped<ShareAccess>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/shares").RequireFeature(FeatureCatalog.Sharing);
        ListShares.Map(routes);
        CreateShare.Map(routes);
        ReadShare.Map(routes);
        RevokeShare.Map(routes);
        OpenSharedFile.Map(routes);
    }
}
