using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using System.Globalization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Features.Work.Jobs;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>
/// Embedding profile 生命週期：列出、重建、啟用、清除已退役的向量（重建與清除只排入背景工作，交易內不做耗時的事）。
/// 每次變更都經 <see cref="AdministrativeWrite"/> 以檢索管理頁（<see cref="FeatureCatalog.KnowledgeRetrieval"/>）的權限執行並稽核。
/// </summary>
internal sealed class ManageEmbeddingProfiles(AppDbContext db, EmbeddingLifecycle lifecycle, AdministrativeWrite write)
{
    public static void MapList(RouteGroupBuilder routes) => routes
        .MapGet("/profiles", (ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.ListAsync(ct))
        .Audited(KnowledgeAudit.ProfileRead)
        .WithName("ListEmbeddingProfiles");

    public static void MapRebuild(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/rebuild", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.RebuildAsync(user.Id, id, ct))
        .Audited(KnowledgeAudit.ProfileRebuilt)
        .WithName("RebuildEmbeddingProfile");

    public static void MapActivate(RouteGroupBuilder routes) => routes
        .MapPost("/profiles/{id:int}/activate", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.ActivateAsync(user.Id, id, ct))
        .Audited(KnowledgeAudit.ProfileActivated)
        .WithName("ActivateEmbeddingProfile");

    public static void MapClear(RouteGroupBuilder routes) => routes
        .MapDelete("/profiles/{id:int}/vectors", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.ClearAsync(user.Id, id, ct))
        .Audited(KnowledgeAudit.ProfileCleared)
        .WithName("ClearRetiredEmbeddingVectors");

    public Task<IReadOnlyList<EmbeddingProfileDto>> ListAsync(CancellationToken ct) => lifecycle.ListAsync(ct);

    public Task<Result<JobDto>> RebuildAsync(Guid actor, int id, CancellationToken ct) => QueueAsync(actor, id, lifecycle.QueueRebuildAsync, ct);

    public Task<Result> ActivateAsync(Guid actor, int id, CancellationToken ct) => LockedAsync(actor, id, () => lifecycle.ActivateCoreAsync(id, ct), ct);

    public Task<Result<JobDto>> ClearAsync(Guid actor, int id, CancellationToken ct) => QueueAsync(actor, id, lifecycle.QueueClearAsync, ct);

    private async Task<Result<JobDto>> QueueAsync(Guid actor, int id, Func<Guid, int, CancellationToken, Task<Result<BackgroundJob>>> queue, CancellationToken ct)
    {
        BackgroundJob? job = null;
        var queued = await LockedAsync(actor, id, async () =>
        {
            var enqueued = await queue(actor, id, ct);
            if (!enqueued.IsSuccess) return enqueued.Error;
            job = enqueued.Value;
            return Result.Success;
        }, ct);
        return queued.IsSuccess ? JobService.Describe(job!) : queued.Error;
    }

    private Task<Result> LockedAsync(Guid actor, int id, Func<Task<Result>> mutation, CancellationToken ct)
        => write.ExecuteAsync(actor, AuditTarget.Of(FeatureCatalog.KnowledgeRetrieval, id.ToString(CultureInfo.InvariantCulture), token => SnapshotAsync(id, token)), mutation, ct);

    /// <summary>所有 profile 的狀態，以及這個 profile 目前的向量數。</summary>
    private async Task<object?> SnapshotAsync(int profile, CancellationToken ct) => new
    {
        profiles = await db.EmbeddingProfiles.AsNoTracking().Select(x => new { x.Id, x.Status, x.ActivatedAt, x.RetiredAt }).ToArrayAsync(ct),
        vectors = await db.ChunkEmbeddings768.CountAsync(x => x.ProfileId == profile, ct) + await db.ChunkEmbeddings1024.CountAsync(x => x.ProfileId == profile, ct)
    };
}
