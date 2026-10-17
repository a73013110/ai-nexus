using AiNexus.Platform.Modules;

namespace AiNexus.Features.AccessControl;

public sealed class AccessControlModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<AccessService>();
    }
}

/// <summary>對話功能的種子資料；預設授權給基本工作區群組，所以一般使用者都能使用。</summary>
internal sealed class AccessControlFeatures() : FeatureSeed(new PlatformFeature(BuiltInAccess.ChatFeature, "對話", "/chat", 10, BuiltInAccess.WorkNavigation, "chat"));

public static class FeaturePolicies
{
    /// <summary>已登入且持有任一指定功能的使用者；每個請求都依當下的授權判斷。</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string policy, params string[] features)
        => services.AddAuthorization(options => options.AddPolicy(policy, p => p.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(features))));

    /// <summary>單一平台功能的標準 policy，名稱為 <c>feature:&lt;id&gt;</c>。</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string feature)
        => services.AddFeaturePolicy(Policies.Prefix + feature, feature);
}
