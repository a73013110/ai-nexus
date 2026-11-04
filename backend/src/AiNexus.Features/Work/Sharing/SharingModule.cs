using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Sharing.Management;
using AiNexus.Features.Work.Sharing.Viewing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Sharing;

/// <summary>綁定帳號的對話與成果快照。刪除來源時，透過領域事件撤銷其分享。</summary>
public sealed class SharingModule : IFeatureModule
{
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/shares").RequireFeature(FeatureCatalog.Sharing)
            .MapSlice<ListShares>()
            .MapSlice<CreateShare>()
            .MapSlice<ReadShare>()
            .MapSlice<RevokeShare>()
            .MapSlice<OpenSharedFile>();
    }
}
