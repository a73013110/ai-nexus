using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Dashboard;

/// <summary>總覽的註冊入口；各區塊由其他模組以 <c>IDashboardSource</c> 自行提供。</summary>
public sealed class DashboardModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(FeatureCatalog.Dashboard);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => GetDashboard.Map(api);
}
