using System.Text;

namespace AiNexus.Features.Diagnostics;

/// <summary>A redacted CSV of the events matching the filter, under its own policy and rate limit. The export is audited first.</summary>
internal static class ExportSystemLogs
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/export", async ([AsParameters] DiagnosticFilter filter, DiagnosticQuery query, HttpContext http, CancellationToken ct) => {
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(Encoding.UTF8.GetBytes(await query.ExportAsync(filter, ct)), "text/csv; charset=utf-8", "ai-nexus-logs.csv");
        }).RequireAuthorization(DiagnosticConfiguration.ExportPolicy).RequireRateLimiting(DiagnosticsModule.ExportRateLimit).WithName("ExportSystemLogs");
}
