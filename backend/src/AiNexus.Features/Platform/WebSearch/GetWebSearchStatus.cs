using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Platform.WebSearch;

internal sealed class GetWebSearchStatus(WebSearchService service)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/tools/web-search", (GetWebSearchStatus handler) => handler.Handle())
        .RequireFeature(FeatureCatalog.Chat)
        .NotAudited("工具可用狀態")
        .WithName("GetWebSearchStatus");

    public WebSearchStatusDto Handle() => service.Status;
}
