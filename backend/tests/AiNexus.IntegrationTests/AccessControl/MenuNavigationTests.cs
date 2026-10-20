using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Platform.Account;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.NavigationApi;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class MenuNavigationTests
{
    [Fact]
    public async Task MembersReceiveTheSeededTreePrunedToTheirPages()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        var tree = (await member.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Navigation;
        // 成員在系統管理只看得到「評測與開發」的兩個評測頁：其他子分群沒有可見頁面而被修剪，系統管理只剩這個分群，由它取代。
        Assert.Equal([FeatureCatalog.Dashboard, BuiltInAccess.WorkNavigation, BuiltInAccess.CollaborationNavigation, "system.evaluation"], tree.Select(x => x.Id));
        var dashboard = tree[0];
        Assert.Equal(("page", "總覽", "dashboard", FeatureCatalog.RouteOf(FeatureCatalog.Dashboard)), (dashboard.Kind, dashboard.Name, dashboard.Icon, dashboard.Route));
        Assert.Equal(["work.chat", "work.projects", "work.files", "work.knowledge", "work.artifacts"], tree[1].Children.Select(x => x.Id));
        Assert.Equal(("group", "AI 工作台"), (tree[1].Kind, tree[1].Name));
        Assert.Equal(["system.evaluation.quality", "system.evaluation.quality-retrieval"], tree[3].Children.Select(x => x.Id));
    }

    [Fact]
    public async Task GroupsLeftWithOneVisibleChildAreReplacedByThatChild()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        using var member = await factory.SignedInAsync("bob");
        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureCatalog.QualityRetrieval}", new FeatureUpdateRequest(null, false))).EnsureSuccessStatusCode();
        var tree = (await member.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Navigation;
        // 單一路徑一路攤到葉節點：評測分群與系統管理都只剩這一頁。
        var quality = Assert.Single(tree, x => x.Id == "system.evaluation.quality");
        Assert.Equal(("page", FeatureCatalog.Quality), (quality.Kind, quality.FeatureId));
        Assert.DoesNotContain(tree, x => x.Id is BuiltInAccess.SystemNavigation or "system.evaluation");

        // 改名後的有效名稱進入選單。
        (await admin.PutAsJsonAsync($"/api/v1/admin/features/{FeatureCatalog.Quality}", new FeatureUpdateRequest("評測", true))).EnsureSuccessStatusCode();
        Assert.Equal("評測", Assert.Single((await member.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Navigation, x => x.Id == "system.evaluation.quality").Name);
    }

    [Fact]
    public async Task GrantOnlyFeaturesStayGrantsButNeverAppearInTheMenu()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var access = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access;
        Assert.Contains(access.Features, x => x.Id == FeatureCatalog.DiagnosticsLogsExport && x.Route == "");
        Assert.DoesNotContain(Flatten(access.Navigation), x => x.FeatureId == FeatureCatalog.DiagnosticsLogsExport);
        Assert.Contains(Flatten(access.Navigation), x => x.Id == "system.access.access-control-navigation" && x.Route == FeatureCatalog.RouteOf(FeatureCatalog.AccessControlNavigation));
        // 功能清單依目錄宣告順序。
        Assert.Equal(FeatureCatalog.Grants.Select(x => x.Id).Where(id => access.Features.Any(f => f.Id == id)), access.Features.Select(x => x.Id));
    }

    [Fact]
    public async Task LinksFollowTheirAudiencesAndTheCurrentAllowList()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        using var member = await factory.SignedInAsync("bob");
        var everyone = await CreateAsync(admin, Link("公告", $"https://{LinkHost}/news"));
        var admins = await CreateAsync(admin, Link("維運手冊", $"https://{LinkHost}/ops", null, BuiltInAccess.AdministratorsGroup) with { OpenInNewTab = false });
        // 允許清單之後移除的主機：直接寫進資料庫模擬設定改掉，產生 /me 時不輸出也不報錯。
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new NavigationItem { Id = "retired-link", Kind = NavigationItem.LinkKind, Name = "舊站", Url = "https://old.example.com/", SortOrder = 900 });
            await db.SaveChangesAsync();
        }

        var adminTree = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Navigation;
        var memberTree = (await member.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Navigation;
        var ops = Assert.Single(adminTree, x => x.Id == admins.Id);
        Assert.Equal(("link", "維運手冊", $"https://{LinkHost}/ops", false, (string?)null), (ops.Kind, ops.Name, ops.Url, ops.OpenInNewTab, ops.Route));
        Assert.Contains(adminTree, x => x.Id == everyone.Id && x.OpenInNewTab);
        Assert.Contains(memberTree, x => x.Id == everyone.Id);
        Assert.DoesNotContain(memberTree, x => x.Id == admins.Id);
        Assert.DoesNotContain(adminTree, x => x.Id == "retired-link");
    }

    [Fact]
    public async Task AccessPreviewCarriesTheSameTreeAsMe()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var me = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var preview = (await admin.GetFromJsonAsync<AccessDto>($"/api/v1/admin/users/{me.Id}/access"))!;
        // 子節點是清單，record 的相等比較只比參考；序列化後比對整棵樹。
        Assert.Equal(JsonSerializer.Serialize(me.Access.Navigation), JsonSerializer.Serialize(preview.Navigation));
        Assert.NotEmpty(preview.Navigation);
    }

    private static IEnumerable<NavigationNodeDto> Flatten(IEnumerable<NavigationNodeDto> nodes) => nodes.SelectMany(x => Flatten(x.Children).Prepend(x));
}
