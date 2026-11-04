using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.WebSearches;

/// <summary>網路搜尋模組的註冊入口。</summary>
public sealed class WebSearchesModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddSettings<WebSearchesOptions, WebSearchesOptionsValidator>(WebSearchesOptions.Section);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapSlice<GetWebSearchStatus>();
}
