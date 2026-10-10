using AiNexus.Features.AccessControl;

namespace AiNexus.Features.WebSearch;

internal sealed class GetWebSearchStatus(WebSearchService service)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/tools/web-search", (GetWebSearchStatus handler) => TypedResults.Ok(handler.Handle()))
        .RequireAuthorization(Policies.Chat)
        .WithName("GetWebSearchStatus");

    public WebSearchStatusDto Handle() => service.Status;
}
