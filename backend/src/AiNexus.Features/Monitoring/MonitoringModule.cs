using System.Threading.RateLimiting;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Monitoring;

/// <summary>Live presence, request traffic and dependency health of this instance. Each use case has its own file.</summary>
public sealed class MonitoringModule : IFeatureModule
{
    public const string Feature = "monitoring", Policy = Policies.Prefix + Feature;
    public const string PresenceRateLimit = "presence";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<MonitoringOptions>().BindConfiguration("Monitoring").ValidateOnStart();
        services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
        services.AddSingleton<RuntimeTraffic>();
        services.AddSingleton<DependencyCatalog>();
        services.AddSingleton<MonitoringStreams>();
        services.AddTransient<TrafficHttpHandler>();
        services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<TrafficHttpHandler>());
        services.AddHostedService<RuntimeSampler>();
        services.AddFeaturePolicy(Feature);
        services.AddRateLimiter(options => options.AddPolicy(PresenceRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirst(SessionIdentity.UserId)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
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

internal sealed class MonitoringOptionsValidator : IValidateOptions<MonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, MonitoringOptions options)
        => options.Valid() ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid monitoring capacity or timing.");
}
