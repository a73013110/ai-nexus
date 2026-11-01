using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Platform.WebSearches;

/// <summary>網路搜尋模組的註冊入口。</summary>
public sealed class WebSearchesModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddDashboardSource<WebSearchDashboard>();
        builder.Services.AddSettings<WebSearchesOptions, WebSearchesOptionsValidator>(WebSearchesOptions.Section);
        builder.Services.AddScoped<WebSearchService>();
        builder.Services.AddScoped<IWebSearchProvider, WebSearchProvider>();
        builder.Services.AddMonitoringSource<WebSearchMonitoring>();
    }

    public static void MapEndpoints(RouteGroupBuilder api) => GetWebSearchStatus.Map(api);
}
