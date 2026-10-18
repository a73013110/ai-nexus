using AiNexus.Features.AccessControl.Management;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// 授權資料與存取管理：角色、功能、選單群組的編輯，以及所有模組的行政變更共用的稽核（<see cref="AdministrativeAudit"/>）。
/// 不依賴任何其他業務模組。
/// </summary>
public sealed class AccessControlModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<AccessService>();
        services.AddScoped<AdministrativeAudit>();
        services.AddScoped<AdministrativeReadAudit>();
        services.AddSingleton<AdministrativeWriteLock>();
        services.AddFeaturePolicy(FeatureIds.Admin);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.Admin).WithTags("Administration");
        SaveRole.Map(admin);
        SaveFeature.Map(admin);
        CreateNavigationGroup.Map(admin);
        UpdateNavigationGroup.Map(admin);
        DeleteNavigationGroup.Map(admin);
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
