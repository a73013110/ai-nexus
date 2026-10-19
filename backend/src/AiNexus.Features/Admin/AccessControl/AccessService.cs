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

    /// <summary>功能在完整選單上的位置（<see cref="NavigationTree.Sections"/>），給管理與稽核目錄的授權清單照側欄分組。</summary>
    public async Task<IReadOnlyList<NavigationSectionDto>> NavigationSectionsAsync(CancellationToken ct)
        => NavigationTree.Sections(await NavigationItemsAsync(ct));

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
        // 一次讀出授權與範圍：同一功能可能由多個群組授予，各帶一個範圍。
        var granted = await (from grant in db.Set<RoleGroupFeature>()
                             join feature in db.Set<Feature>() on grant.FeatureId equals feature.Id
                             where enabledGroups.Contains(grant.GroupId) && feature.Enabled
                             select new { Feature = feature, grant.Scope }).AsNoTracking().ToListAsync(ct);
        var rows = granted.Select(x => x.Feature).DistinctBy(x => x.Id);
        // 目錄不支援的範圍（舊資料、目錄縮減後）與 null 都不算：失敗即拒絕，沒有範圍就沒有資料。
        var scopes = granted.Where(x => x.Scope is not null && FeatureCatalog.Find(x.Feature.Id)?.Scopes.Contains(x.Scope) == true)
            .GroupBy(x => x.Feature.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => (IReadOnlySet<string>)x.Select(y => y.Scope!).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        return new(roles, groups, [.. FeatureDto.InCatalogOrder(rows).Select(FeatureDto.From)], []) { Scopes = scopes };
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
