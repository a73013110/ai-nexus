using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class EndpointConventionTests
{
    // Deny-by-default is the fallback policy; this keeps every endpoint's access an explicit, reviewable decision.
    [Fact]
    public async Task EveryEndpointDeclaresAuthorizationOrAnonymousAccess()
    {
        await using var factory = new NexusFactory();
        _ = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.NotEmpty(endpoints);
        var undeclared = endpoints
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(e => e.RoutePattern.RawText).ToList();
        Assert.True(undeclared.Count == 0, "Endpoints without an explicit authorization decision: " + string.Join(", ", undeclared));
    }

    [Fact]
    public async Task OperationIdsAreUnique()
    {
        await using var factory = new NexusFactory();
        _ = factory.CreateClient();
        var names = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName).OfType<string>().ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
