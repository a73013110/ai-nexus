using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.WebSearch;

/// <summary>網路搜尋模組的註冊入口。</summary>
public sealed class WebSearchModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddDashboardSource<WebSearchDashboard>();
        builder.Services.AddSettings<WebSearchOptions, WebSearchOptionsValidator>(WebSearchOptions.Section);
        builder.Services.AddScoped<WebSearchService>();
        builder.Services.AddScoped<IWebSearchProvider, WebSearchProvider>();
    }

    public static void MapEndpoints(RouteGroupBuilder api) => GetWebSearchStatus.Map(api);
}
