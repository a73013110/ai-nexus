using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Host;

public sealed class EndpointConventionTests
{
    // 預設拒絕是 fallback policy；此測試確保每個 endpoint 的存取權都是明確且可審查的決定。
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

    // 政策名稱拼錯只會在請求時才失敗，這裡改為在測試階段就攔下。
    [Fact]
    public async Task EveryReferencedPolicyIsRegistered()
    {
        await using var factory = new NexusFactory();
        _ = factory.CreateClient();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var names = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .SelectMany(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>()).Select(a => a.Policy).OfType<string>().Distinct().ToList();
        Assert.NotEmpty(names);
        foreach (var name in names) Assert.True(await provider.GetPolicyAsync(name) is not null, "Unregistered authorization policy: " + name);
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

    // 每個 request body 都要有 RequestValidator<T>，或明確標示為何規則留在 handler 中。
    [Fact]
    public async Task EveryRequestBodyHasAValidatorOrAStatedReason()
    {
        await using var factory = new NexusFactory();
        _ = factory.CreateClient();
        var registered = factory.Services.GetRequiredService<IServiceProviderIsService>();
        var bodies = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType).OfType<Type>()
            .Where(t => t.Namespace?.StartsWith("AiNexus.", StringComparison.Ordinal) == true).Distinct().ToList();
        Assert.NotEmpty(bodies);
        var validated = bodies.ToDictionary(t => t, t => registered.IsService(typeof(IValidator<>).MakeGenericType(t)));
        var reasons = bodies.ToDictionary(t => t, t => t.GetCustomAttributes(typeof(ValidatedInHandlerAttribute), false).OfType<ValidatedInHandlerAttribute>().SingleOrDefault()?.Reason);
        var missing = bodies.Where(t => !validated[t] && string.IsNullOrWhiteSpace(reasons[t])).Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        var both = bodies.Where(t => validated[t] && reasons[t] is not null).Select(t => t.FullName).Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Request bodies need a RequestValidator<T> or [ValidatedInHandler(reason)]:\n" + string.Join('\n', missing));
        Assert.True(both.Count == 0, "Request bodies with a validator must not also be marked [ValidatedInHandler]:\n" + string.Join('\n', both));
    }
}
