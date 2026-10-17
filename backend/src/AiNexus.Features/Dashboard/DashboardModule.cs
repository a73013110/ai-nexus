using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Dashboard;

/// <summary>總覽的註冊入口；各區塊由其他模組以 <c>IDashboardSource</c> 自行提供。</summary>
public sealed class DashboardModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(FeatureIds.Dashboard);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => GetDashboard.Map(api);
}

internal sealed class DashboardFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Dashboard, "總覽", "/dashboard", 5, GroupId: null, "dashboard"));
