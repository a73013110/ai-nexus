using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.WebSearch;

public sealed class WebSearchModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddSettings<WebSearchOptions, WebSearchOptionsValidator>(WebSearchOptions.Section);
        builder.Services.AddScoped<WebSearchService>();
        builder.Services.AddScoped<IWebSearchProvider, WebSearchProvider>();
    }

    public static void MapEndpoints(RouteGroupBuilder api) => GetWebSearchStatus.Map(api);
}
