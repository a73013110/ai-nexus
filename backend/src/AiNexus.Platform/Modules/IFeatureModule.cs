namespace AiNexus.Platform.Modules;

/// <summary>
/// A business module owns its services, options, policies and endpoints. The host composes modules from one explicit
/// list, so Program.cs never changes when a feature is added and each module's wiring lives next to its code.
/// </summary>
public interface IFeatureModule
{
    /// <summary>Registers the module's services, options and authorization policies.</summary>
    static abstract void AddServices(IHostApplicationBuilder builder);

    /// <summary>Maps endpoints under the authenticated <c>/api/v1</c> group.</summary>
    static virtual void MapEndpoints(RouteGroupBuilder api) { }

    /// <summary>Maps endpoints that must stay reachable before sign-in; each one declares its own authorization.</summary>
    static virtual void MapPublicEndpoints(IEndpointRouteBuilder app) { }
}
