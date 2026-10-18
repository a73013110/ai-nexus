using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>診斷事件的持久化儲存、管理員日誌查詢與瀏覽器問題回報；寫日誌本身屬於平台層。</summary>
public sealed class DiagnosticsModule : IFeatureModule
{
    public const string ClientIssueRateLimit = "client-issues", QueryRateLimit = "diagnostic-query", ExportRateLimit = "diagnostic-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSingleton<IDiagnosticStore, DiagnosticStore>();
        services.AddScoped<DiagnosticQuery>();
        services.AddSingleton<ClientIssueDeduplication>();
        services.AddMonitoringSource<LogPipelineMonitoring>();
        foreach (var feature in new[] { DiagnosticConfiguration.Query, DiagnosticConfiguration.Detail, DiagnosticConfiguration.Export })
            services.AddFeaturePolicy(feature);
        services.AddRateLimiter(options => options.AddPerUserLimit(ClientIssueRateLimit, 10).AddPerUserLimit(QueryRateLimit, 60).AddPerUserLimit(ExportRateLimit, 2));
    }

    // 端點註冊順序即公開的 OpenAPI 順序，調整會改變對外文件。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        // 日誌讀取有速率限制，且不進一般請求日誌（查日誌不該再產生日誌）；稽核由 DiagnosticQuery 負責。
        var logs = api.MapGroup("/admin/logs").RequireAuthorization(DiagnosticConfiguration.QueryPolicy).WithTags("System logs").RequireRateLimiting(QueryRateLimit)
            .WithMetadata(new SuppressSuccessfulRequestLog());
        QuerySystemLogs.Map(logs);
        GetSystemLogDetail.Map(logs);
        ExportSystemLogs.Map(logs);
        ReportClientIssue.Map(api);
    }
}
