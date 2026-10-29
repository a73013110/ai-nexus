using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 有效授權，直接從 SQL 讀取。同一個 HTTP 請求內每位使用者最多讀一次；下一個請求會重新讀，所以撤銷對之後的每個請求都會生效。
/// 背景工作，以及長駐請求為定期檢查建立的新 scope，都不是該請求自己的 scope，因此每次都重新讀取。
/// </summary>
public sealed class AccessService(AppDbContext db, IActiveUsers activeUsers, IHttpContextAccessor http, IServiceProvider scope)
{
    private readonly Dictionary<Guid, AccessDto> grants = [];

    /// <summary>清除本請求已讀取的授權。授權可能被寫入改變之後，或必須讀到最新 SQL 結果的重新檢查之前呼叫。</summary>
    public void Invalidate() => grants.Clear();

    public async Task<AccessDto> ForUserAsync(Guid user, CancellationToken ct)
    {
        var memoize = ReferenceEquals(http.HttpContext?.RequestServices, scope);
        if (memoize && grants.TryGetValue(user, out var cached)) return cached;
        var value = await ReadAsync(user, ct);
        if (memoize) grants[user] = value;
        return value;
    }

    /// <summary>使用者目前是否持有 <paramref name="feature"/>（同 <see cref="ForUserAsync"/>，同一請求內共用讀取結果）。</summary>
    public async Task<bool> HoldsAsync(Guid user, string feature, CancellationToken ct)
        => (await ForUserAsync(user, ct)).Features.Any(x => x.Id == feature);

    /// <summary>目前持有 <paramref name="feature"/> 的使用者（規則同 <see cref="ForUserAsync"/>），給背景工作決定通知誰。</summary>
    public async Task<IReadOnlyList<Guid>> HoldersAsync(string feature, CancellationToken ct)
        => await (from userId in activeUsers.Ids
                  join assignment in db.Set<UserRole>() on userId equals assignment.UserId
                  join role in db.Set<Role>() on assignment.RoleId equals role.Id
                  join link in db.Set<RoleGroupRole>() on role.Id equals link.RoleId
                  join roleGroup in db.Set<RoleGroup>() on link.GroupId equals roleGroup.Id
                  join grant in db.Set<RoleGroupFeature>() on roleGroup.Id equals grant.GroupId
                  join granted in db.Set<Feature>() on grant.FeatureId equals granted.Id
                  where role.Enabled && roleGroup.Enabled && granted.Enabled && granted.Id == feature
                  select userId).Distinct().ToListAsync(ct);

    /// <summary>所有選單節點（平面清單、含外部連結的可見群組），同層依 <c>SortOrder</c>、再依 Id；管理端清單與選單樹共用。</summary>
    public async Task<IReadOnlyList<NavigationItemDto>> NavigationItemsAsync(CancellationToken ct)
    {
        var items = await db.Set<NavigationItem>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var audiences = (await db.Set<NavigationItemAudience>().AsNoTracking().ToListAsync(ct))
            .ToLookup(x => x.NavigationItemId, x => x.GroupId, StringComparer.Ordinal);
        return [.. items.Select(x => NavigationItemDto.From(x, [.. audiences[x.Id].Order(StringComparer.Ordinal)]))];
    }

    /// <summary>功能在完整選單上的位置（<see cref="NavigationTree.Sections"/>），給管理與稽核目錄的授權清單照側欄分組。</summary>
    public async Task<IReadOnlyList<NavigationSectionDto>> NavigationSectionsAsync(CancellationToken ct)
        => NavigationTree.Sections(await NavigationItemsAsync(ct));

    private async Task<AccessDto> ReadAsync(Guid user, CancellationToken ct)
    {
        if (!await activeUsers.Ids.AnyAsync(x => x == user, ct)) return new([], [], [], []);
        var roleIds = db.Set<UserRole>().Where(x => x.UserId == user).Select(x => x.RoleId);
        var roles = await db.Set<Role>().AsNoTracking().Where(x => roleIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var groupIds = GroupIds(user);
        var groups = await db.Set<RoleGroup>().AsNoTracking().Where(x => groupIds.Contains(x.Id) && x.Enabled)
            .OrderBy(x => x.Id).Select(x => new AccessItemDto(x.Id, x.Name)).ToListAsync(ct);
        var enabledGroups = groups.Select(x => x.Id).ToArray();
        // 一次讀出授權與範圍：同一功能可能由多個群組授予，各帶一個成員範圍與一個主管範圍。
        var granted = await (from grant in db.Set<RoleGroupFeature>()
                             join feature in db.Set<Feature>() on grant.FeatureId equals feature.Id
                             where enabledGroups.Contains(grant.GroupId) && feature.Enabled
                             select new { Feature = feature, grant.Scope, grant.ManagerScope }).AsNoTracking().ToListAsync(ct);
        var rows = granted.Select(x => x.Feature).DistinctBy(x => x.Id);
        // 兩種範圍的代碼不重複，放進同一個集合；目錄不支援的值（舊資料、目錄縮減後）與 null 都不算：失敗即拒絕，沒有範圍就沒有資料。
        var scopes = granted.SelectMany(x => SupportedScopes(x.Feature.Id, x.Scope, x.ManagerScope).Select(scope => (x.Feature.Id, Scope: scope)))
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => (IReadOnlySet<string>)x.Select(y => y.Scope).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        return new(roles, groups, [.. FeatureDto.InCatalogOrder(rows).Select(FeatureDto.From)], []) { GrantedScopes = scopes };
    }

    private static IEnumerable<string> SupportedScopes(string featureId, string? scope, string? managerScope)
    {
        if (FeatureCatalog.Find(featureId) is not { } feature) yield break;
        if (scope is not null && feature.Scopes.Contains(scope)) yield return scope;
        if (managerScope is not null && feature.ManagerScopes.Contains(managerScope)) yield return managerScope;
    }

    // 啟用的使用者經由啟用的角色取得的啟用群組。SQL 報表（儲存上限、管理列表）用同一條規則的 accesscontrol.udf_UserGroups；
    // 授權刻意留在程式（ADR 0011），「啟用」的定義跟著 IActiveUsers 走，改這裡要一起改函式。
    private IQueryable<string> GroupIds(Guid user) =>
        from userId in activeUsers.Ids
        where userId == user
        join assignment in db.Set<UserRole>() on userId equals assignment.UserId
        join role in db.Set<Role>() on assignment.RoleId equals role.Id
        join link in db.Set<RoleGroupRole>() on role.Id equals link.RoleId
        join roleGroup in db.Set<RoleGroup>() on link.GroupId equals roleGroup.Id
        where role.Enabled && roleGroup.Enabled
        select roleGroup.Id;
}

/// <summary>已啟用且未刪除的使用者 id。由 Identity 實作，讓 AccessControl 不必直接讀取使用者表就能過濾授權。</summary>
public interface IActiveUsers
{
    IQueryable<Guid> Ids { get; }
}

/// <summary>由 Identity 的 <c>FeatureAuthorizationHandler</c> 依登入者當下的授權評估。</summary>
public sealed record FeatureRequirement(params string[] FeatureIds) : IAuthorizationRequirement;
