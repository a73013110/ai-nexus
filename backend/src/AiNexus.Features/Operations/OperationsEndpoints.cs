using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using EDoc.Core.Database.Interfaces;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Operations;

public static class OperationsEndpoints
{
    public static void MapOperations(this RouteGroupBuilder api)
    {
        api.MapGet("/status", async (GenerationScheduler scheduler, IDbHelper<INexusDatabase> db, IOptions<AdAuthenticationOptions> auth, CancellationToken ct) =>
        {
            var connected = await db.QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct) == 1;
            return Results.Ok(new StatusDto(connected && scheduler.Ready ? "ready" : "unavailable", auth.Value.Mode.ToLowerInvariant(), scheduler.QueueDepth, scheduler.Generating));
        }).WithName("GetStatus").WithTags("Operations").Produces<StatusDto>();
    }
}
