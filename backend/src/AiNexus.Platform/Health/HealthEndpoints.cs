using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AiNexus.Platform.Health;

/// <summary>
/// <c>/health/live</c> only says the process answers; <c>/health/ready</c> runs the checks tagged <see cref="ReadyTag"/>
/// (the database) and answers 200 or 503. Both are anonymous, so the body never names a check or its failure.
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
