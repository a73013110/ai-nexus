using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Operations;

public sealed record AdminRoleDto(string Id, string Name, bool Enabled, IReadOnlyList<string> GroupIds, int UserCount);

/// <summary>
/// <c>Scopes</c>：已授予且帶資料範圍的功能 → 所有成員的範圍（ADR 0017）；沒有範圍概念的功能不出現。
/// <c>ManagerScopes</c>：另外給主管範圍的功能 → 主管範圍（ADR 0022）；不另外的功能不出現。
/// </summary>
public sealed record AdminGroupDto(string Id, string Name, bool Enabled, IReadOnlyList<string> FeatureIds, GroupPolicyRequest? Policy, IReadOnlyDictionary<string, string> Scopes,
    IReadOnlyDictionary<string, string> ManagerScopes);

/// <summary>管理端的功能列；<c>Name</c> 是覆寫或目錄預設名稱，<c>Route</c>、<c>Icon</c> 由 <see cref="FeatureCatalog"/> 填入（同 <see cref="FeatureDto"/>）。</summary>
public sealed record AdminFeatureDto(string Id, string Name, string Route, bool Enabled, string Icon);

/// <summary><c>NavigationSections</c>：功能在完整選單上的位置，授權清單據此照側欄分組（持有使用者或存取控制任一頁面的人都要用，不能依賴選單管理授權）。</summary>
public sealed record AdminCatalogDto(IReadOnlyList<AdminRoleDto> Roles, IReadOnlyList<AdminGroupDto> Groups, IReadOnlyList<AdminFeatureDto> Features, IReadOnlyList<ModelDto> Models,
    IReadOnlyList<NavigationSectionDto> NavigationSections);

/// <summary>所有角色、群組（含模型策略）與功能（依目錄宣告順序）、功能的選單位置，以及已設定的模型。選單節點本身另由 <c>/admin/navigation-items</c> 提供。</summary>
internal sealed class GetAdminCatalog(AppDbContext db, ModelCatalog catalog, AccessService access)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/catalog", async (GetAdminCatalog handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
        .NotAudited("管理頁的固定目錄")
        .WithName("GetAdminCatalog");

    public async Task<AdminCatalogDto> HandleAsync(CancellationToken ct)
    {
        var roles = await db.Set<Role>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var groups = await db.Set<RoleGroup>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var features = FeatureDto.InCatalogOrder(await db.Set<Feature>().AsNoTracking().ToListAsync(ct));
        var links = (await db.Set<RoleGroupRole>().AsNoTracking().ToListAsync(ct)).ToLookup(x => x.RoleId, x => x.GroupId);
        var grants = (await db.Set<RoleGroupFeature>().AsNoTracking().ToListAsync(ct)).ToLookup(x => x.GroupId);
        var policies = await db.Set<GroupModelPolicy>().AsNoTracking().ToDictionaryAsync(x => x.GroupId, ct);
        var counts = await db.Set<UserRole>().GroupBy(x => x.RoleId).Select(x => new { RoleId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);
        var roleDtos = roles
            .Select(x => new AdminRoleDto(x.Id, x.Name, x.Enabled, links[x.Id].ToArray(), counts.GetValueOrDefault(x.Id)))
            .ToArray();
        // 沒有模型策略列時 Policy 為 null，代表該群組未設定策略。
        var groupDtos = groups
            .Select(x => new AdminGroupDto(
                x.Id,
                x.Name,
                x.Enabled,
                grants[x.Id].Select(y => y.FeatureId).ToArray(),
                policies.TryGetValue(x.Id, out var p) ? GroupPolicyRequest.From(p) : null,
                grants[x.Id].Where(y => y.Scope != null).ToDictionary(y => y.FeatureId, y => y.Scope!, StringComparer.Ordinal),
                grants[x.Id].Where(y => y.ManagerScope != null).ToDictionary(y => y.FeatureId, y => y.ManagerScope!, StringComparer.Ordinal)))
            .ToArray();
        var featureDtos = features.Select(ToDto).ToArray();
        return new(roleDtos, groupDtos, featureDtos, (await catalog.ProfilesAsync(ct)).Select(x => x.ToDto()).ToArray(), await access.NavigationSectionsAsync(ct));
    }

    private static AdminFeatureDto ToDto(Feature row)
    {
        var dto = FeatureDto.From(row);
        return new(dto.Id, dto.Name, dto.Route, row.Enabled, dto.Icon);
    }
}
