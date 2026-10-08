using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
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

    // Request bodies move from ad-hoc checks in services to validators module by module; this list may only shrink.
    [Fact]
    public async Task RequestBodiesWithoutValidatorsNeverGrow()
    {
        await using var factory = new NexusFactory();
        _ = factory.CreateClient();
        var registered = factory.Services.GetRequiredService<IServiceProviderIsService>();
        var actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType).OfType<Type>()
            .Where(t => t.Namespace?.StartsWith("AiNexus.", StringComparison.Ordinal) == true)
            .Where(t => !registered.IsService(typeof(IValidator<>).MakeGenericType(t)))
            .Select(t => t.FullName!).ToHashSet(StringComparer.Ordinal);
        var baseline = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "request-validators.baseline.txt"))
            .Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('#')).ToHashSet(StringComparer.Ordinal);
        var added = actual.Except(baseline).Order(StringComparer.Ordinal).ToList();
        var removed = baseline.Except(actual).Order(StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0, "New request bodies need a RequestValidator<T>:\n" + string.Join('\n', added));
        Assert.True(removed.Count == 0, "These request bodies are validated now; delete them from request-validators.baseline.txt:\n" + string.Join('\n', removed));
    }
}
