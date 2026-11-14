using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Flow;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>
/// 一筆授權。<paramref name="Login"/> 是寫進 Gitea 的登入名（人員取目前的 AD 帳號）；<paramref name="EndedAt"/> 有值表示已結束、協作者還在移除中。
/// </summary>
public sealed record RepositoryGrantDto(
    Guid Id, RepositoryPersonDto? User, string? Login, string Permission, DateOnly? ExpiresOn, string Source, RepositoryPersonDto? GrantedBy,
    DateTimeOffset GrantedAt, string? Reason, string SyncState, string? SyncError, DateTimeOffset? EndedAt);

public sealed record RepositoryPendingRequestDto(Guid InstanceId, RepositoryPersonDto Applicant, string Permission, DateOnly? ExpiresOn, string Reason, DateTimeOffset SubmittedAt);

/// <summary>Gitea 上有、這裡沒有授權的協作者；<paramref name="User"/> 是登入名對得上的人員。</summary>
public sealed record RepositoryCollaboratorDto(string Login, string Permission, DateTimeOffset ObservedAt, RepositoryPersonDto? User);

/// <summary>授權、審核中的申請與未登記協作者只給負責人與管理員，其他人是空清單。</summary>
public sealed record ManagedRepositoryDetailDto(
    ManagedRepositoryDto Repository, long RowVersion, IReadOnlyList<RepositoryGrantDto> Grants, IReadOnlyList<RepositoryPendingRequestDto> Requests,
    IReadOnlyList<RepositoryCollaboratorDto> Unregistered);

/// <summary>一個儲存庫的權限詳情；看不到的儲存庫回找不到。會稽核。</summary>
internal sealed class GetManagedRepository(AppDbContext db, FlowService flow, AccessService access, AuditTrail audit) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetManagedRepository handler, CancellationToken ct) => handler.HandleAsync(id, user.Id, ct))
        .Audited(RepositoriesAudit.AccessRead)
        .WithName("GetManagedRepository");

    public async Task<Result<ManagedRepositoryDetailDto>> HandleAsync(Guid id, Guid actor, CancellationToken ct)
    {
        var administrator = await access.HoldsAsync(actor, FeatureCatalog.RepositoriesAccessManage, ct);
        var visible = ManagedRepositoryRows.Visible(db, actor, administrator).Where(x => x.Id == id);
        if ((await ManagedRepositoryRows.ReadAsync(db, flow, visible, actor, administrator, ct)).SingleOrDefault() is not { } repository)
            return RepositoriesErrors.ManagedNotFound;
        audit.Describe(id);
        var version = await db.ManagedRepositories.Where(x => x.Id == id).Select(x => x.RowVersion).SingleAsync(ct);
        if (repository.Manager == RepositoryManager.None) return new ManagedRepositoryDetailDto(repository, version, [], [], []);
        return new ManagedRepositoryDetailDto(repository, version, await GrantsAsync(id, ct), await RequestsAsync(id, ct), await UnregisteredAsync(id, ct));
    }

    private async Task<IReadOnlyList<RepositoryGrantDto>> GrantsAsync(Guid id, CancellationToken ct)
    {
        var rows = await (from grant in db.RepositoryGrants.AsNoTracking()
                          where grant.RepositoryId == id && (grant.EndedAt == null || grant.SyncState != RepositorySyncStates.Applied)
                          join user in db.Users on grant.UserId equals (Guid?)user.Id into users
                          from user in users.DefaultIfEmpty()
                          join grantor in db.Users on grant.GrantedById equals (Guid?)grantor.Id into grantors
                          from grantor in grantors.DefaultIfEmpty()
                          select new
                          {
                              Grant = grant, UserName = user == null ? null : user.DisplayName, Account = user == null ? grant.Login : user.AdAccount,
                              GrantorName = grantor == null ? null : grantor.DisplayName,
                          }).ToListAsync(ct);
        return [.. rows.OrderBy(x => x.Grant.EndedAt is not null).ThenBy(x => x.UserName ?? x.Account, StringComparer.CurrentCulture).Select(x => new RepositoryGrantDto(
            x.Grant.Id, x.Grant.UserId is { } user ? new(user, x.UserName ?? "") : null, x.Account, x.Grant.Permission,
            x.Grant.ExpiresAt is { } at ? RepositoryAccess.ExpiryDate(at) : null, x.Grant.Source,
            x.Grant.GrantedById is { } grantor ? new(grantor, x.GrantorName ?? "") : null, x.Grant.GrantedAt, x.Grant.Reason, x.Grant.SyncState,
            x.Grant.SyncError, x.Grant.EndedAt))];
    }

    private async Task<IReadOnlyList<RepositoryPendingRequestDto>> RequestsAsync(Guid id, CancellationToken ct)
    {
        var rows = await (from instance in flow.Instances(RepositoryAccessFlow.Kind)
                          join request in db.RepositoryAccessRequests on instance.SubjectId equals request.Id
                          join user in db.Users on request.UserId equals user.Id
                          where instance.Status == FlowStatuses.Pending && request.RepositoryId == id
                          orderby instance.SubmittedAt
                          select new { instance.Id, user.DisplayName, request.UserId, request.Permission, request.ExpiresAt, request.Reason, instance.SubmittedAt })
            .ToListAsync(ct);
        return [.. rows.Select(x => new RepositoryPendingRequestDto(x.Id, new(x.UserId, x.DisplayName), x.Permission,
            x.ExpiresAt is { } at ? RepositoryAccess.ExpiryDate(at) : null, x.Reason, x.SubmittedAt))];
    }

    private async Task<IReadOnlyList<RepositoryCollaboratorDto>> UnregisteredAsync(Guid id, CancellationToken ct)
    {
        var rows = await (from collaborator in db.RepositoryCollaborators.AsNoTracking()
                          where collaborator.RepositoryId == id
                          join user in db.Users on collaborator.Login equals user.AdAccount into users
                          from user in users.DefaultIfEmpty()
                          orderby collaborator.Login
                          select new { collaborator.Login, collaborator.Permission, collaborator.ObservedAt, UserId = user == null ? (Guid?)null : user.Id, Name = user == null ? null : user.DisplayName })
            .ToListAsync(ct);
        return [.. rows.DistinctBy(x => x.Login).Select(x => new RepositoryCollaboratorDto(x.Login, x.Permission, x.ObservedAt,
            x.UserId is { } user ? new(user, x.Name ?? "") : null))];
    }
}
