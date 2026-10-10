using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Platform.Configuration;

namespace AiNexus.Features.Monitoring;

/// <summary>Live presence, request traffic and dependency health of this instance. Each use case has its own file.</summary>
public sealed class MonitoringModule : IFeatureModule
{
    public const string Feature = "monitoring", Policy = Policies.Prefix + Feature;
    public const string PresenceRateLimit = "presence";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<MonitoringOptions, MonitoringOptionsValidator>(MonitoringOptions.Section);
        services.AddSingleton<RuntimeTraffic>();
        services.AddDomainEventHandler<UserSignedOut, LeaveSignedOutSession>();
        services.AddSingleton<DependencyCatalog>();
        services.AddSingleton<MonitoringStreams>();
        services.AddTransient<TrafficHttpHandler>();
        services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<TrafficHttpHandler>());
        services.AddHostedService<RuntimeSampler>();
        services.AddFeaturePolicy(Feature);
        services.AddRateLimiter(options => options.AddPerUserLimit(PresenceRateLimit, 120));
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        HeartbeatPresence.Map(api);
        LeavePresence.Map(api);
        // Monitoring reads share the log query rate limit, are audited and kept out of the request log.
        var monitor = api.MapGroup("/admin/monitoring").RequireAuthorization(Policy).RequireRateLimiting(AiNexus.Features.Diagnostics.DiagnosticsModule.QueryRateLimit)
            .WithMetadata(new SuppressSuccessfulRequestLog()).WithTags("Runtime monitoring");
        GetMonitoringSnapshot.Map(monitor);
        ExportMonitoringSnapshot.Map(monitor);
        StreamMonitoringSnapshots.Map(monitor);
    }
}

public static class RuntimeTrafficMiddlewareExtensions
{
    /// <summary>Outermost: measures every request, including ones the diagnostic and security layers reject.</summary>
    public static IApplicationBuilder UseRuntimeTraffic(this IApplicationBuilder app) => app.UseMiddleware<RuntimeTrafficMiddleware>();
}

internal sealed class MonitoringFeatures() : FeatureSeed(new PlatformFeature(MonitoringModule.Feature, "即時監控", "/admin/monitoring", 91, AdministratorsOnly: true));
