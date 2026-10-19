using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 有效授權，直接從 SQL 讀取。同一個 HTTP 請求內每位使用者最多讀一次；下一個請求會重新讀，所以撤銷對之後的每個請求都會生效。
/// 背景工作，以及長駐請求為定期檢查建立的新 scope，都不是該請求自己的 scope，因此每次都重新讀取。
/// </summary>
public sealed class AccessService(AppDbContext db, IActiveUsers activeUsers, IHttpContextAccessor http, IServiceProvider scope, IOptions<AccessControlOptions> settings)
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
    /// 在 <see cref="ForUserAsync"/> 的結果上加入使用者看得到的選單樹（<see cref="NavigationTree.Build"/>），供 <c>/me</c> 與存取預覽使用；
    /// 授權檢查直接呼叫 <see cref="ForUserAsync"/>，略過選單查詢。外部連結每次都再比對允許清單：主機移出清單（設定改完、站台重新啟動）後，已存的連結就不再輸出。
    /// </summary>
    public async Task<AccessDto> WithNavigationAsync(Guid user, CancellationToken ct)
    {
        var access = await ForUserAsync(user, ct);
        var tree = NavigationTree.Build(await NavigationItemsAsync(ct), access.Features, access.Groups.Select(x => x.Id), settings.Value.NavigationLinkHosts);
        return access with { Navigation = tree };
    }

    /// <summary>所有選單節點（平面清單、含外部連結的可見群組），同層依 <c>SortOrder</c>、再依 Id；管理端清單與選單樹共用。</summary>
    public async Task<IReadOnlyList<NavigationItemDto>> NavigationItemsAsync(CancellationToken ct)
    {
        var items = await db.Set<NavigationItem>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var audiences = (await db.Set<NavigationItemAudience>().AsNoTracking().ToListAsync(ct))
            .ToLookup(x => x.NavigationItemId, x => x.GroupId, StringComparer.Ordinal);
        return [.. items.Select(x => NavigationItemDto.From(x, [.. audiences[x.Id].Order(StringComparer.Ordinal)]))];
    }

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
        var rows = await db.Set<Feature>().AsNoTracking().Where(x => featureIds.Contains(x.Id) && x.Enabled).ToListAsync(ct);
        return new(roles, groups, [.. FeatureDto.InCatalogOrder(rows).Select(FeatureDto.From)], []);
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
