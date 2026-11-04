using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Diagnostics;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Monitoring.Presence;
using AiNexus.Features.Admin.Monitoring.Snapshots;
using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Monitoring;

/// <summary>本實例的即時在線、請求流量與相依服務健康度。每個用例各自一個檔案。</summary>
public sealed class MonitoringModule : IFeatureModule
{
    public const string PresenceRateLimit = "presence";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<MonitoringOptions, MonitoringOptionsValidator>(MonitoringOptions.Section);
        services.AddTransient<TrafficHttpHandler>();
        services.ConfigureHttpClientDefaults(client => client.AddHttpMessageHandler<TrafficHttpHandler>());
        services.AddRateLimiter(options => options.AddPerUserLimit(PresenceRateLimit, 120));
    }

    // 端點順序即為已發布的 OpenAPI 順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapSlice<HeartbeatPresence>()
            .MapSlice<LeavePresence>();
        // 監控讀取與日誌查詢共用速率限制、會被稽核，且不列入請求日誌，避免監控自己的查詢洗版日誌。
        api.MapGroup("/admin/monitoring").RequireFeature(FeatureCatalog.Monitoring).RequireRateLimiting(DiagnosticsModule.QueryRateLimit)
            .WithMetadata(new SuppressSuccessfulRequestLog())
            .MapSlice<GetMonitoringSnapshot>()
            .MapSlice<ExportMonitoringSnapshot>()
            .MapSlice<StreamMonitoringSnapshots>();
    }
}

public static class RuntimeTrafficMiddlewareExtensions
{
    /// <summary>必須最外層註冊：才能量測每個請求，包括被診斷或安全層拒絕的請求。</summary>
    public static IApplicationBuilder UseRuntimeTraffic(this IApplicationBuilder app) => app.UseMiddleware<RuntimeTrafficMiddleware>();
}
