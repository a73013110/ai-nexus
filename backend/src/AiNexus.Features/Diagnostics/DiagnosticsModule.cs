using System.Threading.RateLimiting;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.RateLimiting;

namespace AiNexus.Features.Diagnostics;

/// <summary>Persistent diagnostic store, administrator log queries and browser issue intake. Logging itself is a platform concern.</summary>
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
        services.AddRateLimiter(options =>
        {
            foreach (var (policy, permits) in new[] { (ClientIssueRateLimit, 10), (QueryRateLimit, 60), (ExportRateLimit, 2) })
                options.AddPolicy(policy, http => RateLimitPartition.GetFixedWindowLimiter(PerUser(http),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
    }

    public static void MapEndpoints(RouteGroupBuilder api) => api.MapDiagnostics();

    private static string PerUser(HttpContext http)
        => http.User.FindFirst(SessionIdentity.UserId)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
