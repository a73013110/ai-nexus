using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Diagnostics;

/// <summary>Queue, file and SQL health of the diagnostic pipeline. The read is audited by <see cref="DiagnosticQuery"/>.</summary>
internal static class GetSystemLogHealth
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/health", async (DiagnosticQuery query, CancellationToken ct) => Results.Ok(await query.HealthAsync(ct)))
        .WithName("GetSystemLogHealth").Produces<DiagnosticHealthDto>();
}
