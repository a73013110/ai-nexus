using AiNexus.Features.Identity;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Diagnostics;

/// <summary>Queue, file and SQL health of the diagnostic pipeline. The read is audited by <see cref="DiagnosticQuery"/>.</summary>
internal sealed class GetSystemLogHealth(DiagnosticQuery query)
{
    public static void Map(RouteGroupBuilder logs) => logs
        .MapGet("/health", async (ICurrentUser user, GetSystemLogHealth handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("GetSystemLogHealth");

    public Task<DiagnosticHealthDto> HandleAsync(Guid actor, CancellationToken ct) => query.HealthAsync(actor, ct);
}
