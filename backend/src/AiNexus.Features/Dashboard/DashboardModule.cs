using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Dashboard;

public sealed class DashboardModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(FeatureIds.Dashboard);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => GetDashboard.Map(api);
}

internal sealed class DashboardFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Dashboard, "總覽", "/dashboard", 5));
