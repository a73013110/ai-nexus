namespace AiNexus.Features.Diagnostics;

/// <summary>One page of stored diagnostic events matching the filter. <see cref="DiagnosticQuery"/> validates, audits and pages.</summary>
internal static class QuerySystemLogs
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("", async ([AsParameters] DiagnosticFilter filter, DiagnosticQuery query, CancellationToken ct) => Results.Ok(await query.ListAsync(filter, ct)))
        .WithName("QuerySystemLogs").Produces<DiagnosticPage>();
}
