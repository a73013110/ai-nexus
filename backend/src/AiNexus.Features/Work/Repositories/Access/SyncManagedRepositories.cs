using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>管理員要求立刻同步一輪（例如剛在 Gitea 建了儲存庫、或調整了團隊）。會稽核。</summary>
internal sealed class SyncManagedRepositories(ScheduledTasks tasks) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/sync", (ICurrentUser user, SyncManagedRepositories handler, CancellationToken ct) => handler.HandleAsync(user.Id, ct))
        .RequireFeature(FeatureCatalog.RepositoriesAccessManage)
        .Audited(RepositoriesAudit.ManagedRepositorySynced)
        .WithName("SyncManagedRepositories");

    public Task<Result> HandleAsync(Guid actor, CancellationToken ct) => tasks.RequestRunAsync(RepositoryAccessSyncTask.Definition.Key, actor, null, ct);
}
