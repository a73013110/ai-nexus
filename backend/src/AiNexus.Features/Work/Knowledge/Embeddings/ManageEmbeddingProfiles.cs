using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using System.Globalization;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Features.Work.Jobs;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>
/// Embedding profile 生命週期：列出、重建、啟用、清除已退役的向量（重建與清除只排入背景工作，交易內不做耗時的事）。
/// 每次變更都經 <see cref="AdministrativeWrite"/> 稽核，並在 <see cref="KnowledgeLocks.EmbeddingProfile"/> 排隊。
/// </summary>
internal sealed class ManageEmbeddingProfiles(AppDbContext db, EmbeddingLifecycle lifecycle, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes)
    {
        routes.MapGet("/profiles", (ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.ListAsync(ct))
            .Audited(KnowledgeAudit.ProfileRead)
            .WithName("ListEmbeddingProfiles");

        routes.MapPost("/profiles/{id:int}/rebuild", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.RebuildAsync(user.Id, id, ct))
            .Audited(KnowledgeAudit.ProfileRebuilt)
            .WithName("RebuildEmbeddingProfile");

        routes.MapPost("/profiles/{id:int}/activate", (int id, ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.ActivateAsync(id, ct))
            .Audited(KnowledgeAudit.ProfileActivated)
            .WithName("ActivateEmbeddingProfile");

        routes.MapDelete("/profiles/{id:int}/vectors", (int id, ICurrentUser user, ManageEmbeddingProfiles handler, CancellationToken ct) =>
            handler.ClearAsync(user.Id, id, ct))
            .Audited(KnowledgeAudit.ProfileCleared)
            .WithName("ClearRetiredEmbeddingVectors");
    }

    public Task<IReadOnlyList<EmbeddingProfileDto>> ListAsync(CancellationToken ct) => lifecycle.ListAsync(ct);

    public Task<Result<JobDto>> RebuildAsync(Guid actor, int id, CancellationToken ct) => QueueAsync(actor, id, lifecycle.QueueRebuildAsync, ct);

    public async Task<Result> ActivateAsync(int id, CancellationToken ct)
    {
        var begun = await write.BeginAsync(Target(id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var activated = await lifecycle.ActivateCoreAsync(id, ct);
        return activated.IsSuccess ? await transaction.CommitAsync(ct) : activated.Error;
    }

    public Task<Result<JobDto>> ClearAsync(Guid actor, int id, CancellationToken ct) => QueueAsync(actor, id, lifecycle.QueueClearAsync, ct);

    private async Task<Result<JobDto>> QueueAsync(Guid actor, int id, Func<Guid, int, CancellationToken, Task<Result<BackgroundJob>>> queue, CancellationToken ct)
    {
        var begun = await write.BeginAsync(Target(id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var job = await queue(actor, id, ct);
        if (!job.IsSuccess) return job.Error;

        return await transaction.CommitAsync(() => JobService.Describe(job.Value), ct);
    }

    private AuditTarget Target(int id)
        => AuditTarget.Of(id.ToString(CultureInfo.InvariantCulture), token => SnapshotAsync(id, token)).Locking(KnowledgeLocks.EmbeddingProfile);

    /// <summary>所有 profile 的狀態，以及這個 profile 目前的向量數。</summary>
    private async Task<object?> SnapshotAsync(int profile, CancellationToken ct) => new
    {
        profiles = await db.EmbeddingProfiles.AsNoTracking().Select(x => new { x.Id, x.Status, x.ActivatedAt, x.RetiredAt }).ToArrayAsync(ct),
        vectors = await db.ChunkEmbeddings768.CountAsync(x => x.ProfileId == profile, ct) + await db.ChunkEmbeddings1024.CountAsync(x => x.ProfileId == profile, ct)
    };
}
