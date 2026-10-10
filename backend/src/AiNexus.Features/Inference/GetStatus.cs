using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using Microsoft.Extensions.Options;
using AiNexus.Features.Identity.Authentication;

namespace AiNexus.Features.Inference;

public sealed record StatusDto(string Storage, string Authentication, int QueueDepth, bool Generating);

/// <summary>Whether storage answers and generation is ready, the sign-in mode and the generation queue depth.</summary>
internal sealed class GetStatus(GenerationScheduler scheduler, ISqlDatabase<NexusDbContext> db, IOptions<AdAuthenticationOptions> auth)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapGet("/status", async (GetStatus handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .WithName("GetStatus").WithTags("Operations");

    public async Task<StatusDto> HandleAsync(CancellationToken ct)
    {
        var connected = await db.QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct) == 1;
        return new StatusDto(connected && scheduler.Ready ? "ready" : "unavailable", auth.Value.Mode.ToLowerInvariant(), scheduler.QueueDepth, scheduler.Generating);
    }
}
