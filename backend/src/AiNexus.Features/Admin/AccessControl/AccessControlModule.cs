using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.AccessControl.Scopes;
using AiNexus.Platform.Accounts;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 授權資料與存取管理：角色、功能、選單的編輯，以及所有模組的行政變更共用的稽核（<see cref="AdministrativeAudit"/>）。
/// 不依賴任何其他業務模組。
/// </summary>
public sealed class AccessControlModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<AccessService>();
        // IUnitMembership 由 Organization 註冊（AccessControl 不依賴它）。
        services.AddScoped<IDataScope, DataScopeService>();
        services.AddScoped<AccessView>();
        services.AddScoped<AdministrativeAudit>();
        services.AddAccountReviewSource<RoleHolderAccountReview>();
        services.AddScoped<AdministrativeReadAudit>();
        services.AddSingleton<AdministrativeWriteLock>();
        services.AddFeaturePolicy(FeatureCatalog.AccessControlRoles);
        services.AddFeaturePolicy(FeatureCatalog.AccessControlGroups);
        services.AddFeaturePolicy(FeatureCatalog.AccessControlFeatures);
        services.AddFeaturePolicy(FeatureCatalog.AccessControlNavigation);
        services.AddFeaturePolicy(Policies.AccessCatalog, FeatureCatalog.IdentityUsers, FeatureCatalog.AccessControlRoles,
            FeatureCatalog.AccessControlGroups, FeatureCatalog.AccessControlFeatures, FeatureCatalog.AccessControlNavigation);
        services.AddSettings<AccessControlOptions, AccessControlOptionsValidator>(AccessControlOptions.Section);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        SaveRole.Map(api.MapGroup("/admin").RequireAuthorization(Policies.AccessControlRoles));
        SaveFeature.Map(api.MapGroup("/admin").RequireAuthorization(Policies.AccessControlFeatures));
        var navigation = api.MapGroup("/admin").RequireAuthorization(Policies.AccessControlNavigation);
        ListNavigationItems.Map(navigation);
        CreateNavigationItem.Map(navigation);
        // 字面路徑 /order 的路由優先於 /{id}，所以兩個 PUT 不會互相攔截；伺服器產生的 Id 是 GUID、Seed 也沒有叫 order 的節點。
        ReorderNavigationItems.Map(navigation);
        UpdateNavigationItem.Map(navigation);
        DeleteNavigationItem.Map(navigation);
    }
}

public static class FeaturePolicies
{
    /// <summary>已登入且持有任一指定功能的使用者；每個請求都依當下的授權判斷。</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string policy, params string[] features)
        => services.AddAuthorization(options => options.AddPolicy(policy, p => p.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(features))));

    /// <summary>單一平台功能的標準 policy，名稱為 <c>feature:&lt;id&gt;</c>。</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string feature)
        => services.AddFeaturePolicy(Policies.Prefix + feature, feature);
}
