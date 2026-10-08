using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.WebSearch;

public sealed class WebSearchModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<WebSearchOptions>().BindConfiguration("Tools:WebSearch").ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WebSearchOptions>, WebSearchOptionsValidator>();
        builder.Services.AddScoped<WebSearchService>();
        builder.Services.AddScoped<IWebSearchProvider, WebSearchProvider>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
        => api.MapGet("/tools/web-search", (WebSearchService service) => Results.Ok(service.Status))
            .RequireAuthorization(Policies.Chat).WithName("GetWebSearchStatus").Produces<WebSearchStatusDto>();
}

internal sealed class WebSearchOptionsValidator : IValidateOptions<WebSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, WebSearchOptions x)
        => x.Provider is "searxng" or "brave" && x.TimeoutSeconds is >= 2 and <= 30 && x.MaxResults is >= 1 and <= 8 && x.MaxDailyRequests is >= 1 and <= 10000
           && Uri.TryCreate(x.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid web search settings.");
}
