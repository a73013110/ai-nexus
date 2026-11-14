using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Office.Flow;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>最近一輪同步；只給管理員看。</summary>
public sealed record RepositorySyncDto(bool Running, bool Requested, DateTimeOffset? LastFinishedAt, string? LastStatus, string? LastSummary);

/// <summary><paramref name="Enabled"/> 為 false 時還沒設定服務帳號，不能申請與授予。</summary>
public sealed record ManagedRepositoriesDto(bool Enabled, bool Administrator, IReadOnlyList<ManagedRepositoryDto> Items, RepositorySyncDto? Sync);

/// <summary>可申請的儲存庫與自己的權限；管理員看全部並看到同步狀態。</summary>
internal sealed class ListManagedRepositories(AppDbContext db, FlowService flow, AccessService access, ScheduledTasks tasks, IOptions<GiteaOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ICurrentUser user, ListManagedRepositories handler, CancellationToken ct) => handler.HandleAsync(user.Id, ct))
        .NotAudited("只列出儲存庫與自己的權限；誰有權限在詳情才看得到，那裡會稽核")
        .WithName("ListManagedRepositories");

    public async Task<ManagedRepositoriesDto> HandleAsync(Guid actor, CancellationToken ct)
    {
        var administrator = await access.HoldsAsync(actor, FeatureCatalog.RepositoriesAccessManage, ct);
        var items = await ManagedRepositoryRows.ReadAsync(db, flow, ManagedRepositoryRows.Visible(db, actor, administrator), actor, administrator, ct);
        var state = administrator ? await tasks.GetStateAsync(RepositoryAccessSyncTask.Definition.Key, ct) : null;
        var sync = state is null ? null : new RepositorySyncDto(state.Running, state.Requested, state.LastRun?.FinishedAt, state.LastRun?.Status, state.LastRun?.Summary);
        return new(options.Value.AccessEnabled, administrator, items, sync);
    }
}
