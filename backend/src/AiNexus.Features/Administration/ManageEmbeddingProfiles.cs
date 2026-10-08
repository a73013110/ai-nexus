using AiNexus.Features.Knowledge;
using AiNexus.Features.Operations;

namespace AiNexus.Features.Administration;

/// <summary>Embedding profile lifecycle: list, rebuild, activate and clear retired vectors. <see cref="EmbeddingLifecycle"/> audits each change.</summary>
internal static class ManageEmbeddingProfiles
{
    public static void MapList(RouteGroupBuilder routes) => routes
        .MapGet("/profiles", async (EmbeddingLifecycle service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)))
        .WithName("ListEmbeddingProfiles").Produces<IReadOnlyList<EmbeddingProfileDto>>();

    public static void MapRebuild(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/rebuild", async (int id, EmbeddingLifecycle service, CancellationToken ct) => Results.Ok(await service.StartAsync(id, ct)))
        .WithName("RebuildEmbeddingProfile").Produces<JobDto>();

    public static void MapActivate(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/activate", async (int id, EmbeddingLifecycle service, CancellationToken ct) => { await service.ActivateAsync(id, ct); return Results.NoContent(); })
        .WithName("ActivateEmbeddingProfile");

    public static void MapClear(RouteGroupBuilder routes) => routes
        .MapDelete("/profiles/{id:int}/vectors", async (int id, EmbeddingLifecycle service, CancellationToken ct) => { await service.ClearAsync(id, ct); return Results.NoContent(); })
        .WithName("ClearRetiredEmbeddingVectors");
}
