using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.WebSearches;

internal sealed class GetWebSearchStatus(WebSearchService service) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder api) => api
        .MapGet("/tools/web-search", (GetWebSearchStatus handler) => handler.Handle())
        .RequireFeature(FeatureCatalog.Chat)
        .NotAudited("工具可用狀態")
        .WithName("GetWebSearchStatus");

    public WebSearchStatusDto Handle() => service.Status;
}
