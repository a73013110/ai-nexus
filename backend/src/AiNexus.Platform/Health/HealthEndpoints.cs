using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiNexus.Platform.Health;

/// <summary>
/// 應用程式健康檢查端點：
/// <c>/health/live</c> 僅確認處理程序存活並可回應；
/// <c>/health/ready</c> 則執行標記為 <see cref="ReadyTag"/> 的核心相依性檢查（如資料庫連線）並回應 200 或 503。
/// 兩者皆允許匿名存取，回應本體不洩漏任何內部元件名稱或失敗細節。
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IEndpointConventionBuilder MapReadinessCheck(this IEndpointRouteBuilder app) => app
        .MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = (http, report) => http.Response.WriteAsJsonAsync(new { status = report.Status == HealthStatus.Unhealthy ? "unavailable" : "ready" }),
        })
        .AllowAnonymous();
}
