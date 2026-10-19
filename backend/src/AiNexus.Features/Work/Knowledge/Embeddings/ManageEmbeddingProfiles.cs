using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using System.Globalization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Features.Work.Jobs;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>
/// Embedding profile 生命週期：列出、重建、啟用、清除已退役的向量。
/// 每次變更都先取得 knowledge 寫入鎖，再經 <see cref="AdministrativeAudit"/> 以檢索管理頁（<see cref="FeatureCatalog.KnowledgeRetrieval"/>）的權限執行並稽核。
/// </summary>
internal sealed class ManageEmbeddingProfiles(AppDbContext db, EmbeddingLifecycle lifecycle, KnowledgeWriteLock writes, AdministrativeAudit audit)
{
    public static void MapList(RouteGroupBuilder routes) => routes
        .MapGet("/profiles", async (ManageEmbeddingProfiles handler, CancellationToken ct) => TypedResults.Ok(await handler.ListAsync(ct)))
        .WithName("ListEmbeddingProfiles");

    public static void MapRebuild(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/rebuild", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) => handler.RebuildAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("RebuildEmbeddingProfile");

    public static void MapActivate(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/activate", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) => handler.ActivateAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ActivateEmbeddingProfile");

    public static void MapClear(RouteGroupBuilder routes) => routes
        .MapDelete("/profiles/{id:int}/vectors", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) => handler.ClearAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ClearRetiredEmbeddingVectors");

    public Task<IReadOnlyList<EmbeddingProfileDto>> ListAsync(CancellationToken ct) => lifecycle.ListAsync(ct);

    public async Task<Result<JobDto>> RebuildAsync(Guid actor, int id, CancellationToken ct)
    {
        BackgroundJob? job = null;
        var queued = await LockedAsync(actor, "admin.embedding_rebuild", id, async () =>
        {
            var rebuild = await lifecycle.QueueRebuildAsync(actor, id, ct);
            if (!rebuild.IsSuccess) return rebuild.Error;
            job = rebuild.Value;
            return Result.Success;
        }, ct);
        return queued.IsSuccess ? JobService.Describe(job!) : queued.Error;
    }

    public Task<Result> ActivateAsync(Guid actor, int id, CancellationToken ct) => LockedAsync(actor, "admin.embedding_activate", id, () => lifecycle.ActivateCoreAsync(id, ct), ct);

    public Task<Result> ClearAsync(Guid actor, int id, CancellationToken ct) => LockedAsync(actor, "admin.embedding_clear", id, () => lifecycle.ClearCoreAsync(id, ct), ct);

    private async Task<Result> LockedAsync(Guid actor, string action, int id, Func<Task<Result>> mutation, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try { return await audit.MutateAsync(actor, FeatureCatalog.KnowledgeRetrieval, action, null, id.ToString(CultureInfo.InvariantCulture), token => SnapshotAsync(id, token), mutation, ct); }
        finally { writes.Gate.Release(); }
    }

    /// <summary>所有 profile 的狀態，以及這個 profile 目前的向量數。</summary>
    private async Task<object?> SnapshotAsync(int profile, CancellationToken ct) => new
    {
        profiles = await db.Set<EmbeddingProfile>().AsNoTracking().Select(x => new { x.Id, x.Status, x.ActivatedAt, x.RetiredAt }).ToArrayAsync(ct),
        vectors = await db.Set<ChunkEmbedding768>().CountAsync(x => x.ProfileId == profile, ct) + await db.Set<ChunkEmbedding1024>().CountAsync(x => x.ProfileId == profile, ct)
    };
}
