using System.Globalization;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Features.Administration.Retrieval;

/// <summary>
/// Embedding profile lifecycle: list, rebuild, activate and clear retired vectors. Each change holds the knowledge write
/// lock and is audited.
/// </summary>
internal sealed class ManageEmbeddingProfiles(EmbeddingLifecycle lifecycle, KnowledgeWriteLock writes, AdministrativeAudit audit)
{
    public static void MapList(RouteGroupBuilder routes) => routes
        .MapGet("/profiles", async (ManageEmbeddingProfiles handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(ct)))
        .WithName("ListEmbeddingProfiles");

    public static void MapRebuild(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/rebuild", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) => handler.RebuildAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("RebuildEmbeddingProfile");

    public static void MapActivate(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/activate", (int id, ManageEmbeddingProfiles handler, CancellationToken ct) => handler.ActivateAsync(id, ct).ToHttpResultAsync())
        .WithName("ActivateEmbeddingProfile");

    public static void MapClear(RouteGroupBuilder routes) => routes
        .MapDelete("/profiles/{id:int}/vectors", (int id, ManageEmbeddingProfiles handler, CancellationToken ct) => handler.ClearAsync(id, ct).ToHttpResultAsync())
        .WithName("ClearRetiredEmbeddingVectors");

    public Task<IReadOnlyList<EmbeddingProfileDto>> ListAsync(CancellationToken ct) => lifecycle.ListAsync(ct);

    public async Task<Result<JobDto>> RebuildAsync(Guid actor, int id, CancellationToken ct)
    {
        BackgroundJob? job = null;
        var queued = await LockedAsync("admin.embedding_rebuild", id, async () =>
        {
            var rebuild = await lifecycle.QueueRebuildAsync(actor, id, ct);
            if (!rebuild.IsSuccess) return rebuild.Error;
            job = rebuild.Value;
            return Result.Success;
        }, ct);
        return queued.IsSuccess ? JobService.Describe(job!) : queued.Error;
    }

    public Task<Result> ActivateAsync(int id, CancellationToken ct) => LockedAsync("admin.embedding_activate", id, () => lifecycle.ActivateCoreAsync(id, ct), ct);

    public Task<Result> ClearAsync(int id, CancellationToken ct) => LockedAsync("admin.embedding_clear", id, () => lifecycle.ClearCoreAsync(id, ct), ct);

    private async Task<Result> LockedAsync(string action, int id, Func<Task<Result>> mutation, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try { return await audit.TryMutateAsync(action, null, id.ToString(CultureInfo.InvariantCulture), mutation, ct); }
        finally { writes.Gate.Release(); }
    }
}
