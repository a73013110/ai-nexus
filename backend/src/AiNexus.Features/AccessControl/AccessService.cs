using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.AccessControl.Roles;
using AiNexus.Features.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.AccessControl;

/// <summary>
/// 有效授權，直接從 SQL 讀取。同一個 HTTP 請求內每位使用者最多讀一次；下一個請求會重新讀，所以撤銷對之後的每個請求都會生效。
/// 背景工作，以及長駐請求為定期檢查建立的新 scope，都不是該請求自己的 scope，因此每次都重新讀取。
/// </summary>
public sealed class AccessService(NexusDbContext db, IActiveUsers activeUsers, IHttpContextAccessor http, IServiceProvider scope)
{
    private readonly Dictionary<Guid, AccessDto> grants = [];

    /// <summary>清除本請求已讀取的授權。授權可能被寫入改變之後，或必須讀到最新 SQL 結果的重新檢查之前呼叫。</summary>
    public void Invalidate() => grants.Clear();

    public IQueryable<UserGroupGrant> GroupMemberships(IReadOnlyList<Guid> users) =>
        (from userId in activeUsers.Ids join assignment in db.Set<UserRole>() on userId equals assignment.UserId
         join role in db.Set<Role>() on assignment.RoleId equals role.Id
         join link in db.Set<RoleGroupRole>() on role.Id equals link.RoleId
         join roleGroup in db.Set<RoleGroup>() on link.GroupId equals roleGroup.Id
         where users.Contains(userId) && role.Enabled && roleGroup.Enabled
         select new UserGroupGrant { UserId = userId, GroupId = roleGroup.Id }).Distinct();
    public async Task<AccessDto> ForUserAsync(Guid user, CancellationToken ct)
    {
        var memoize = ReferenceEquals(http.HttpContext?.RequestServices, scope);
        if (memoize && grants.TryGetValue(user, out var cached)) return cached;
        var value = await ReadAsync(user, ct);
        if (memoize) grants[user] = value;
        return value;
    }

    /// <summary>
    /// 在 <see cref="ForUserAsync"/> 的結果上加入使用者功能所需的選單分群（<see cref="NavigationTree.VisibleGroups"/>），供繪製選單的 payload 使用；
    /// 授權檢查直接呼叫 <see cref="ForUserAsync"/>，略過分群查詢。
    /// </summary>
    public async Task<AccessDto> WithNavigationAsync(Guid user, CancellationToken ct)
    {
        var access = await ForUserAsync(user, ct);
        return access with { NavigationGroups = NavigationTree.VisibleGroups(await NavigationGroupsAsync(ct), access.Features) };
    }

    /// <summary>依顯示順序列出所有選單分群，供需要排列任意功能的檢視使用（例如目錄、稽核歷史），而非單一使用者的選單。</summary>
    public async Task<IReadOnlyList<NavigationGroupDto>> NavigationGroupsAsync(CancellationToken ct) =>
        await db.Set<NavigationGroup>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new NavigationGroupDto(x.Id, x.ParentId, x.Name, x.SortOrder)).ToArrayAsync(ct);

    private async Task<AccessDto> ReadAsync(Guid user, CancellationToken ct)
    {
        if (!await activeUsers.Ids.AnyAsync(x => x == user, ct)) return new([], [], [], []);
        var roleIds = db.Set<UserRole>().Where(x => x.UserId == user).Select(x => x.RoleId);
        var roles = await db.Set<Role>().AsNoTracking().Where(x => roleIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var groupIds = GroupMemberships([user]).Select(x => x.GroupId);
        var groups = await db.Set<RoleGroup>().AsNoTracking().Where(x => groupIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var enabledGroups = groups.Select(x => x.Id).ToArray();
        var featureIds = db.Set<RoleGroupFeature>().Where(x => enabledGroups.Contains(x.GroupId)).Select(x => x.FeatureId);
        var features = await db.Set<Feature>().AsNoTracking().Where(x => featureIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new FeatureDto(x.Id, x.Name, x.Route, x.GroupId, x.Icon, x.SortOrder))
            .ToListAsync(ct);
        return new(roles, groups, features, []);
    }
}

/// <summary>已啟用且未刪除的使用者 id。由 Identity 實作，讓 AccessControl 不必直接讀取使用者表就能過濾授權。</summary>
public interface IActiveUsers
{
    IQueryable<Guid> Ids { get; }
}

public sealed class UserGroupGrant
{
    public Guid UserId { get; init; }
    public string GroupId { get; init; } = "";
}

/// <summary>由 Identity 的 <c>FeatureAuthorizationHandler</c> 依登入者當下的授權評估。</summary>
public sealed record FeatureRequirement(params string[] FeatureIds) : IAuthorizationRequirement;
