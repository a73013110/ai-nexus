using AiNexus.Features.AccessControl;
using AiNexus.Features.Diagnostics;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Monitoring.Presence;
using AiNexus.Features.Monitoring.Snapshots;
using AiNexus.Features.Monitoring.Traffic;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Events;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Monitoring;

/// <summary>本實例的即時在線、請求流量與相依服務健康度。每個用例各自一個檔案。</summary>
public sealed class MonitoringModule : IFeatureModule
{
    public const string Feature = "monitoring", Policy = Policies.Prefix + Feature;
    public const string PresenceRateLimit = "presence";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<MonitoringOptions, MonitoringOptionsValidator>(MonitoringOptions.Section);
        services.AddSingleton<RuntimeTraffic>();
        services.AddSingleton<SubsystemReadings>();
        services.AddSingleton<MonitoringSnapshots>();
        services.AddDomainEventHandler<UserSignedOut, LeaveSignedOutSession>();
        services.AddSingleton<DependencyCatalog>();
        services.AddSingleton<MonitoringStreams>();
        services.AddTransient<TrafficHttpHandler>();
        services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<TrafficHttpHandler>());
        services.AddHostedService<RuntimeSampler>();
        services.AddFeaturePolicy(Feature);
        services.AddRateLimiter(options => options.AddPerUserLimit(PresenceRateLimit, 120));
    }

    // 端點順序即為已發布的 OpenAPI 順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        HeartbeatPresence.Map(api);
        LeavePresence.Map(api);
        // 監控讀取與日誌查詢共用速率限制、會被稽核，且不列入請求日誌，避免監控自己的查詢洗版日誌。
        var monitor = api.MapGroup("/admin/monitoring").RequireAuthorization(Policy).RequireRateLimiting(DiagnosticsModule.QueryRateLimit)
            .WithMetadata(new SuppressSuccessfulRequestLog()).WithTags("Runtime monitoring");
        GetMonitoringSnapshot.Map(monitor);
        ExportMonitoringSnapshot.Map(monitor);
        StreamMonitoringSnapshots.Map(monitor);
    }
}

public static class RuntimeTrafficMiddlewareExtensions
{
    /// <summary>必須最外層註冊：才能量測每個請求，包括被診斷或安全層拒絕的請求。</summary>
    public static IApplicationBuilder UseRuntimeTraffic(this IApplicationBuilder app) => app.UseMiddleware<RuntimeTrafficMiddleware>();
}

internal sealed class MonitoringFeatures() : FeatureSeed(new PlatformFeature(MonitoringModule.Feature, "即時監控", "/admin/monitoring", 93, BuiltInAccess.SystemNavigation, "activity", AdministratorsOnly: true));
