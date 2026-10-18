using AiNexus.Data.App.AccessControl;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.AccessControl;

/// <summary>
/// 內建存取資料只寫在手寫的 Seed，程式另有一份常數；兩邊在範本資料庫（dacpac＋Seed）上雙向比對，
/// fork 改了任一邊而沒同步時在這裡失敗，而不是部署後才發現登入沒有功能或授權永遠不成立。
/// </summary>
public sealed class BuiltInAccessSeedTests
{
    private const string Seed = "database/App/Seed/accesscontrol.sql";
    private const string Constants = "backend/src/AiNexus.Features/AccessControl/BuiltInAccess.cs";
    private const string Features = "backend/src/AiNexus.Features/AccessControl/Policies.cs（FeatureIds）";

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
                .. Missing("功能", FeatureIds.All, features, Features),
                .. Missing("角色→群組", BuiltInAccess.RoleGroups.Select(x => $"{x.Role}→{x.Group}"), roleGroups.Select(x => $"{x.RoleId}→{x.GroupId}"), Constants),
                .. Missing("群組→功能", BuiltInAccess.GroupFeatures.Select(x => $"{x.Group}→{x.Feature}"), groupFeatures.Select(x => $"{x.GroupId}→{x.FeatureId}"), Constants),
            ];
            Assert.True(missing.Length == 0, $"{Seed} 缺少程式依賴的內建存取資料；補進 Seed，或確定不再需要時從右側檔案移除常數：\n" + string.Join("\n", missing));
        });
    }

    [Fact]
    public async Task EveryFeatureInSeedIsKnownToTheCode()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var unknown = (await db.Set<Feature>().Select(x => x.Id).ToListAsync()).Except(FeatureIds.All, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            Assert.True(unknown.Count == 0,
                $"{Seed} 有程式不認得的功能 id；在 {Features} 加常數（前端的 FeatureId 型別由 OpenAPI 產生，記得重產契約），或從 Seed 移除：\n"
                + string.Join("\n", unknown));
        });
    }

    private static IEnumerable<string> Missing(string kind, IEnumerable<string> expected, IEnumerable<string> actual, string source)
    {
        var present = actual.ToHashSet(StringComparer.Ordinal);
        return expected.Where(id => !present.Contains(id)).Select(id => $"- {kind} '{id}'（定義在 {source}）");
    }
}
