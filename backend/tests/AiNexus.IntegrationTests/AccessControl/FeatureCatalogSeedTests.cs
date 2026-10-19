using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
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
            var navigation = await db.Set<NavigationGroup>().Select(x => x.Id).ToListAsync();
            var features = await db.Set<Feature>().Select(x => x.Id).ToListAsync();
            var roleGroups = (await db.Set<RoleGroupRole>().Select(x => new { x.RoleId, x.GroupId }).ToListAsync()).Select(x => (x.RoleId, x.GroupId));
            var groupFeatures = (await db.Set<RoleGroupFeature>().Select(x => new { x.GroupId, x.FeatureId }).ToListAsync()).Select(x => (x.GroupId, x.FeatureId));

            string[] missing =
            [
                .. Missing("角色", BuiltInAccess.Roles, roles, Constants),
                .. Missing("群組", BuiltInAccess.Groups, groups, Constants),
                .. Missing("選單分群", BuiltInAccess.NavigationGroups, navigation, Constants),
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
