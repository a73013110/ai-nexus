using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using EDoc.Core.Database.Interfaces;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Operations;

public sealed record StatusDto(string Storage, string Authentication, int QueueDepth, bool Generating);

/// <summary>Whether storage answers and generation is ready, the sign-in mode and the generation queue depth.</summary>
internal static class GetStatus
{
    public static void Map(RouteGroupBuilder api) => api
        .MapGet("/status", async (GenerationScheduler scheduler, IDbHelper<INexusDatabase> db, IOptions<AdAuthenticationOptions> auth, CancellationToken ct) =>
        {
            var connected = await db.QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct) == 1;
            return Results.Ok(new StatusDto(connected && scheduler.Ready ? "ready" : "unavailable", auth.Value.Mode.ToLowerInvariant(), scheduler.QueueDepth, scheduler.Generating));
        }).WithName("GetStatus").WithTags("Operations").Produces<StatusDto>();
}
