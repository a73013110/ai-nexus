using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Diagnostics;

/// <summary>
/// Persistent diagnostic store, administrator log queries and browser issue intake. Logging itself is a platform concern.
/// Each use case has its own file.
/// </summary>
public sealed class DiagnosticsModule : IFeatureModule
{
    public const string ClientIssueRateLimit = "client-issues", QueryRateLimit = "diagnostic-query", ExportRateLimit = "diagnostic-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSingleton<IDiagnosticStore, DiagnosticStore>();
        services.AddScoped<DiagnosticQuery>();
        services.AddSingleton<ClientIssueDeduplication>();
        foreach (var feature in new[] { DiagnosticConfiguration.Query, DiagnosticConfiguration.Detail, DiagnosticConfiguration.Export })
            services.AddFeaturePolicy(feature);
        services.AddRateLimiter(options => options.AddPerUserLimit(ClientIssueRateLimit, 10).AddPerUserLimit(QueryRateLimit, 60).AddPerUserLimit(ExportRateLimit, 2));
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        // Log reads are rate limited and kept out of the request log; each read is audited by DiagnosticQuery.
        var logs = api.MapGroup("/admin/logs").RequireAuthorization(DiagnosticConfiguration.QueryPolicy).WithTags("System logs").RequireRateLimiting(QueryRateLimit)
            .WithMetadata(new SuppressSuccessfulRequestLog());
        QuerySystemLogs.Map(logs);
        GetSystemLogHealth.Map(logs);
        GetSystemLogDetail.Map(logs);
        ExportSystemLogs.Map(logs);
        ReportClientIssue.Map(api);
    }
}
