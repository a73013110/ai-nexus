using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.AccessControl;

/// <summary>
/// 內建存取資料只寫在手寫的 Seed，程式另有功能目錄與內建常數；兩邊在範本資料庫（dacpac＋Seed）上雙向比對，
/// fork 改了任一邊而沒同步時在這裡失敗，而不是部署後才發現登入沒有功能、授權永遠不成立，或資料庫有前端不認得的功能。
/// </summary>
public sealed class FeatureCatalogSeedTests
{
    private const string Seed = "database/App/Seed/accesscontrol.sql";
    private const string Constants = "backend/src/AiNexus.Features/Admin/AccessControl/BuiltInAccess.cs";
    private const string Catalog = "backend/src/AiNexus.Features/Admin/AccessControl/FeatureCatalog.cs";

    [Fact]
    public async Task SeedContainsEveryIdAndRelationTheCodeDependsOn()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var roles = await db.Set<Role>().Select(x => x.Id).ToListAsync();
            var groups = await db.Set<RoleGroup>().Select(x => x.Id).ToListAsync();
            var navigation = await db.Set<NavigationItem>().Where(x => x.Kind == NavigationItem.GroupKind).Select(x => x.Id).ToListAsync();
            var features = await db.Set<Feature>().Select(x => x.Id).ToListAsync();
            var roleGroups = (await db.Set<RoleGroupRole>().Select(x => new { x.RoleId, x.GroupId }).ToListAsync()).Select(x => (x.RoleId, x.GroupId));
            var groupFeatures = (await db.Set<RoleGroupFeature>().Select(x => new { x.GroupId, x.FeatureId }).ToListAsync()).Select(x => (x.GroupId, x.FeatureId));

            string[] missing =
            [
                .. Missing("角色", BuiltInAccess.Roles, roles, Constants),
                .. Missing("群組", BuiltInAccess.Groups, groups, Constants),
                .. Missing("預設選單分群（NavigationItems，kind＝group）", BuiltInAccess.NavigationGroups, navigation, Constants),
                .. Missing("功能（目錄的頁面與動作）", FeatureCatalog.Grants.Select(x => x.Id), features, Catalog),
                .. Missing("角色→群組", BuiltInAccess.RoleGroups.Select(x => $"{x.Role}→{x.Group}"), roleGroups.Select(x => $"{x.RoleId}→{x.GroupId}"), Constants),
                .. Missing("群組→功能", BuiltInAccess.GroupFeatures.Select(x => $"{x.Group}→{x.Feature}"), groupFeatures.Select(x => $"{x.GroupId}→{x.FeatureId}"), Constants),
            ];
            Assert.True(missing.Length == 0, $"{Seed} 缺少程式依賴的內建存取資料；補進 Seed，或確定不再需要時從右側檔案移除：\n" + string.Join("\n", missing));
        });
    }

    [Fact]
    public async Task EveryFeatureInSeedIsAGrantInTheCatalog()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var grants = FeatureCatalog.Grants.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var unknown = (await db.Set<Feature>().Select(x => x.Id).ToListAsync()).Where(id => !grants.Contains(id)).Order(StringComparer.Ordinal).ToList();
            Assert.True(unknown.Count == 0,
                $"{Seed} 有目錄沒有的功能，或把閱讀器寫成了授權；在 {Catalog} 宣告頁面或動作（前端的 FeatureId 與路由目錄由 OpenAPI 產生，記得重產契約），或從 Seed 移除：\n"
                + string.Join("\n", unknown));
        });
    }

    [Fact]
    public async Task EveryCatalogPageIsPlacedInTheDefaultMenuAndPageNodesPointAtPages()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var pages = await db.Set<NavigationItem>().Where(x => x.Kind == NavigationItem.PageKind).Select(x => new { x.Id, x.FeatureId }).ToListAsync();
            var placed = pages.Select(x => x.FeatureId!).ToHashSet(StringComparer.Ordinal);
            var unplaced = FeatureCatalog.All.Where(x => x.Kind == FeatureKind.Page && !placed.Contains(x.Id)).Select(x => $"- {x.Id}").ToList();
            Assert.True(unplaced.Count == 0, $"{Seed} 的預設選單樹沒有放這些頁面，新環境的使用者即使有授權也看不到入口；在 NavigationItems 補上 page 節點：\n" + string.Join("\n", unplaced));
            var notPages = pages.Where(x => FeatureCatalog.Find(x.FeatureId!)?.Kind != FeatureKind.Page).Select(x => $"- {x.Id} → {x.FeatureId}").ToList();
            Assert.True(notPages.Count == 0, $"{Seed} 的 page 節點必須指向 {Catalog} 的頁面（動作與閱讀器沒有選單入口）：\n" + string.Join("\n", notPages));
        });
    }

    [Fact]
    public async Task DefaultMenuSatisfiesTheRulesTheManagementApiEnforces()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var items = await db.Set<NavigationItem>().ToListAsync();
            var groups = items.Where(x => x.Kind == NavigationItem.GroupKind).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            Assert.All(items, x => Assert.True(x.ParentId is null || groups.Contains(x.ParentId), $"{x.Id} 的上層不是分群"));
            var parents = items.ToDictionary(x => x.Id, x => x.ParentId, StringComparer.Ordinal);
            Assert.False(NavigationTree.HasCycle(parents));
            Assert.InRange(NavigationTree.Deepest(parents), 1, NavigationTree.MaxDepth);
        });
    }

    [Fact]
    public async Task SeedGrantsCarryAScopeExactlyWhenTheCatalogDeclaresScopes()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var grants = await db.Set<RoleGroupFeature>().Select(x => new { x.GroupId, x.FeatureId, x.Scope }).ToListAsync();
            var wrong = grants.Where(x => FeatureCatalog.Find(x.FeatureId)?.Scopes is { } supported && (supported.Count == 0 ? x.Scope is not null : x.Scope is null || !supported.Contains(x.Scope)))
                .Select(x => $"- {x.GroupId}→{x.FeatureId}：{x.Scope ?? "NULL"}").ToList();
            Assert.True(wrong.Count == 0, $"{Seed} 的預設授權範圍要符合 {Catalog} 的宣告：有宣告範圍的功能給一個支援的範圍，其他為 NULL（ADR 0017）：\n" + string.Join("\n", wrong));
        });
    }

    [Fact]
    public async Task SeedLeavesNamesToTheCatalog()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var named = await db.Set<Feature>().Where(x => x.Name != null).Select(x => x.Id).ToListAsync();
            Assert.True(named.Count == 0, $"{Seed} 的 Name 是管理員的名稱覆寫，內建列應為 NULL（預設名稱寫在 {Catalog}）：\n" + string.Join("\n", named));
        });
    }

    private static IEnumerable<string> Missing(string kind, IEnumerable<string> expected, IEnumerable<string> actual, string source)
    {
        var present = actual.ToHashSet(StringComparer.Ordinal);
        return expected.Where(id => !present.Contains(id)).Select(id => $"- {kind} '{id}'（定義在 {source}）");
    }
}
