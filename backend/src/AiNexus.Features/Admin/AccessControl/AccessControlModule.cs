using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 授權資料與存取管理：角色、功能、選單的編輯，以及所有模組的行政變更共用的寫入與稽核（<see cref="AdministrativeWrite"/>）。
/// 不依賴任何其他業務模組。
/// </summary>
public sealed class AccessControlModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        // 每個可授予的功能都有標準 policy，新增功能只要宣告在 FeatureCatalog；any-of policy 由需要的模組自行註冊。
        foreach (var feature in FeatureCatalog.Grants) services.AddFeaturePolicy(Policies.Prefix + feature.Id, feature.Id);
        services.AddFeaturePolicy(Policies.AccessCatalog, FeatureCatalog.IdentityUsers, FeatureCatalog.AccessControlRoles,
            FeatureCatalog.AccessControlGroups, FeatureCatalog.AccessControlFeatures, FeatureCatalog.AccessControlNavigation);
        services.AddSettings<AccessControlOptions, AccessControlOptionsValidator>(AccessControlOptions.Section);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/admin").RequireFeature(FeatureCatalog.AccessControlRoles)
            .MapSlice<SaveRole>();
        api.MapGroup("/admin").RequireFeature(FeatureCatalog.AccessControlFeatures)
            .MapSlice<SaveFeature>();
        api.MapGroup("/admin").RequireFeature(FeatureCatalog.AccessControlNavigation)
            .MapSlice<ListNavigationItems>()
            .MapSlice<CreateNavigationItem>()
            // 字面路徑 /order 的路由優先於 /{id}，所以兩個 PUT 不會互相攔截；伺服器產生的 Id 是 GUID、Seed 也沒有叫 order 的節點。
            .MapSlice<ReorderNavigationItems>()
            .MapSlice<UpdateNavigationItem>()
            .MapSlice<DeleteNavigationItem>();
    }
}
