using AiNexus.Features.AccessControl;

namespace AiNexus.Features.WebSearch;

internal static class GetWebSearchStatus
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder api) => api
        .MapGet("/tools/web-search", (WebSearchService service) => TypedResults.Ok(service.Status))
        .RequireAuthorization(Policies.Chat)
        .WithName("GetWebSearchStatus")
        .Produces<WebSearchStatusDto>();
}
