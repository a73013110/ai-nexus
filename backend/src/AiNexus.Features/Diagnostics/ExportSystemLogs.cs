using System.Text;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Diagnostics;

/// <summary>A redacted CSV of the events matching the filter, under its own policy and rate limit. The export is audited first.</summary>
internal sealed class ExportSystemLogs(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/export", ([AsParameters] DiagnosticFilter filter, ICurrentUser user, HttpContext http, ExportSystemLogs handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, filter, ct).ToHttpResultAsync(csv =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "ai-nexus-logs.csv");
            }))
        .RequireAuthorization(DiagnosticConfiguration.ExportPolicy).RequireRateLimiting(DiagnosticsModule.ExportRateLimit).WithName("ExportSystemLogs");

    public Task<Result<string>> HandleAsync(Guid actor, DiagnosticFilter filter, CancellationToken ct) => query.ExportAsync(actor, filter, ct);
}
