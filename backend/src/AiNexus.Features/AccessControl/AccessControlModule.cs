using AiNexus.Platform.Modules;

namespace AiNexus.Features.AccessControl;

public sealed class AccessControlModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<AccessService>();
    }
}

internal sealed class AccessControlFeatures() : FeatureSeed(new PlatformFeature(BuiltInAccess.ChatFeature, "對話", "/chat", 10));

public static class FeaturePolicies
{
    /// <summary>Signed-in users holding any of <paramref name="features"/>; evaluated against current grants on every request.</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string policy, params string[] features)
        => services.AddAuthorization(options => options.AddPolicy(policy, p => p.RequireAuthenticatedUser().AddRequirements(new FeatureRequirement(features))));

    /// <summary>The conventional <c>feature:&lt;id&gt;</c> policy for one platform feature.</summary>
    public static IServiceCollection AddFeaturePolicy(this IServiceCollection services, string feature)
        => services.AddFeaturePolicy(Policies.Prefix + feature, feature);
}
