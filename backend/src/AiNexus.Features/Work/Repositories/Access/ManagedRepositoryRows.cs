using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Office.Flow;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

public sealed record RepositoryPersonDto(Guid Id, string DisplayName);

/// <summary>目前使用者自己在這個儲存庫的有效授權；<paramref name="SyncState"/> 不是 applied 時 Gitea 還沒生效。</summary>
public sealed record RepositoryMyGrantDto(Guid Id, string Permission, DateOnly? ExpiresOn, string SyncState);

/// <summary>
/// 儲存庫一列。<paramref name="PendingInstanceId"/> 是自己審核中的申請（連到簽核頁）；<paramref name="Manager"/> 決定畫面能做什麼。
/// <paramref name="GrantingTeams"/> 有值時，這些團隊的成員不靠協作者也能存取，逐人授權擋不住。
/// <paramref name="MyDirectPermission"/> 是自己在 Gitea 上被直接加為協作者、平台沒有授權紀錄時的權限（上一輪同步看到的）；
/// <paramref name="UnregisteredCount"/> 是這類未納管協作者的人數，只給負責人與管理員，其他人是 0。
/// </summary>
public sealed record ManagedRepositoryDto(
    Guid Id, string Owner, string Name, string? Description, bool IsPrivate, bool IsArchived, bool Missing, bool Requestable, string? GrantingTeams,
    IReadOnlyList<RepositoryPersonDto> Stewards, RepositoryMyGrantDto? MyGrant, Guid? PendingInstanceId, RepositoryManager Manager,
    string? MyDirectPermission, int UnregisteredCount);

/// <summary>清單與詳情共用的讀取：可見範圍與每列的「我」相關欄位。</summary>
internal static class ManagedRepositoryRows
{
    /// <summary>管理員看全部；其他人看開放申請且仍在 Gitea 的、自己有授權的、自己負責的。</summary>
    public static IQueryable<ManagedRepository> Visible(AppDbContext db, Guid actor, bool administrator)
        => administrator ? db.ManagedRepositories : db.ManagedRepositories.Where(x =>
            (x.Requestable && !x.IsArchived && x.MissingSince == null)
            || db.RepositoryGrants.Any(g => g.RepositoryId == x.Id && g.UserId == actor && g.EndedAt == null)
            || db.RepositoryStewards.Any(s => s.RepositoryId == x.Id && s.UserId == actor));

    public static async Task<List<ManagedRepositoryDto>> ReadAsync(
        AppDbContext db, FlowService flow, IQueryable<ManagedRepository> repositories, Guid actor, bool administrator, CancellationToken ct)
    {
        var rows = await repositories.AsNoTracking().OrderBy(x => x.Owner).ThenBy(x => x.Name).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var stewards = (await (from steward in db.RepositoryStewards
                               join user in db.Users on steward.UserId equals user.Id
                               where ids.Contains(steward.RepositoryId)
                               orderby user.DisplayName
                               select new { steward.RepositoryId, user.Id, user.DisplayName }).ToListAsync(ct))
            .ToLookup(x => x.RepositoryId, x => new RepositoryPersonDto(x.Id, x.DisplayName));
        var grants = await db.RepositoryGrants.AsNoTracking().Where(x => ids.Contains(x.RepositoryId) && x.UserId == actor && x.EndedAt == null)
            .ToDictionaryAsync(x => x.RepositoryId, ct);
        var pending = (await (from instance in flow.Instances(RepositoryAccessFlow.Kind)
                              join request in db.RepositoryAccessRequests on instance.SubjectId equals request.Id
                              where instance.Status == FlowStatuses.Pending && request.UserId == actor && ids.Contains(request.RepositoryId)
                              select new { request.RepositoryId, instance.Id }).ToListAsync(ct))
            .DistinctBy(x => x.RepositoryId).ToDictionary(x => x.RepositoryId, x => x.Id);
        // 未納管的協作者（同步記下、平台沒有授權的）：自己是其中之一時照實寫出權限，免得明明打得開卻顯示沒有權限。
        // 同步以 AD 帳號當 Gitea 登入名，比對方式與詳情的未納管名單相同。
        var account = await db.Users.Where(x => x.Id == actor).Select(x => x.AdAccount).SingleOrDefaultAsync(ct);
        var direct = account is null
            ? []
            : (await db.RepositoryCollaborators.AsNoTracking().Where(x => ids.Contains(x.RepositoryId) && x.Login == account)
                .Select(x => new { x.RepositoryId, x.Permission }).ToListAsync(ct))
                .DistinctBy(x => x.RepositoryId).ToDictionary(x => x.RepositoryId, x => x.Permission);
        var unregistered = (await db.RepositoryCollaborators.AsNoTracking().Where(x => ids.Contains(x.RepositoryId))
                .GroupBy(x => x.RepositoryId).Select(x => new { RepositoryId = x.Key, Count = x.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.RepositoryId, x => x.Count);
        return [.. rows.Select(x =>
        {
            var manager = administrator ? RepositoryManager.Administrator
                : stewards[x.Id].Any(s => s.Id == actor) ? RepositoryManager.Steward : RepositoryManager.None;
            return new ManagedRepositoryDto(
                x.Id, x.Owner, x.Name, x.Description, x.IsPrivate, x.IsArchived, x.MissingSince is not null, x.Requestable, x.GrantingTeams,
                [.. stewards[x.Id]],
                grants.TryGetValue(x.Id, out var grant)
                    ? new(grant.Id, grant.Permission, grant.ExpiresAt is { } at ? RepositoryAccess.ExpiryDate(at) : null, grant.SyncState) : null,
                pending.TryGetValue(x.Id, out var instance) ? instance : null,
                manager,
                direct.GetValueOrDefault(x.Id),
                manager == RepositoryManager.None ? 0 : unregistered.GetValueOrDefault(x.Id));
        })];
    }
}
