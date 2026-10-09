using System.Globalization;
using AiNexus.Features.Identity;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Operations;

namespace AiNexus.Features.Administration;

/// <summary>
/// Embedding profile lifecycle: list, rebuild, activate and clear retired vectors. Each change holds the knowledge write
/// lock and is audited.
/// </summary>
internal sealed class ManageEmbeddingProfiles(EmbeddingLifecycle lifecycle, KnowledgeWriteLock writes, AdministrativeAudit audit)
{
    public static void MapList(RouteGroupBuilder routes) => routes
        .MapGet("/profiles", async (EmbeddingLifecycle service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)))
        .WithName("ListEmbeddingProfiles").Produces<IReadOnlyList<EmbeddingProfileDto>>();

    public static void MapRebuild(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/rebuild", async (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) => Results.Ok(await handler.RebuildAsync(user.Id, id, ct)))
        .WithName("RebuildEmbeddingProfile").Produces<JobDto>();

    public static void MapActivate(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/activate", async (int id, ManageEmbeddingProfiles handler, CancellationToken ct) => { await handler.ActivateAsync(id, ct); return Results.NoContent(); })
        .WithName("ActivateEmbeddingProfile");

    public static void MapClear(RouteGroupBuilder routes) => routes
        .MapDelete("/profiles/{id:int}/vectors", async (int id, ManageEmbeddingProfiles handler, CancellationToken ct) => { await handler.ClearAsync(id, ct); return Results.NoContent(); })
        .WithName("ClearRetiredEmbeddingVectors");

    public async Task<JobDto> RebuildAsync(Guid actor, int id, CancellationToken ct)
    {
        BackgroundJob? job = null;
        await LockedAsync("admin.embedding_rebuild", id, async () => job = await lifecycle.QueueRebuildAsync(actor, id, ct), ct);
        return JobService.Describe(job!);
    }

    public Task ActivateAsync(int id, CancellationToken ct) => LockedAsync("admin.embedding_activate", id, () => lifecycle.ActivateCoreAsync(id, ct), ct);

    public Task ClearAsync(int id, CancellationToken ct) => LockedAsync("admin.embedding_clear", id, () => lifecycle.ClearCoreAsync(id, ct), ct);

    private async Task LockedAsync(string action, int id, Func<Task> mutation, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try { await audit.MutateAsync(action, null, id.ToString(CultureInfo.InvariantCulture), mutation, ct); }
        finally { writes.Gate.Release(); }
    }
}
