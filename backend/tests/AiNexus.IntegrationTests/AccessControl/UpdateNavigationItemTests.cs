using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Audit;
using static AiNexus.IntegrationTests.Support.NavigationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class UpdateNavigationItemTests
{
    [Fact]
    public async Task AdministratorEditsContentWithoutMovingTheNode()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync($"{Items}/{BuiltInAccess.CollaborationNavigation}", new NavigationItemUpdateRequest("group", null, " 團隊協作 ", null, true, "share", null))).EnsureSuccessStatusCode();
        var link = await CreateAsync(admin, Link("公告", $"https://{LinkHost}/a", BuiltInAccess.WorkNavigation));
        (await admin.PutAsJsonAsync($"{Items}/{link.Id}", new NavigationItemUpdateRequest("link", null, "人資公告", $"https://{LinkHost}/hr", false, null, [BuiltInAccess.AdministratorsGroup]))).EnsureSuccessStatusCode();
        // page 只能換功能；換成同層沒有的頁面可以。
        (await admin.PutAsJsonAsync($"{Items}/collaboration.jobs", new NavigationItemUpdateRequest("page", FeatureCatalog.Design, null, null, true, null, null))).EnsureSuccessStatusCode();

        var items = await ListAsync(admin);
        var group = Assert.Single(items, x => x.Id == BuiltInAccess.CollaborationNavigation);
        Assert.Equal(("團隊協作", "share", (string?)null, 30), (group.Name, group.Icon, group.ParentId, group.SortOrder));
        var edited = Assert.Single(items, x => x.Id == link.Id);
        Assert.Equal(("人資公告", $"https://{LinkHost}/hr", false, BuiltInAccess.WorkNavigation, link.SortOrder), (edited.Name, edited.Url, edited.OpenInNewTab, edited.ParentId, edited.SortOrder));
        Assert.Equal([BuiltInAccess.AdministratorsGroup], edited.AudienceGroupIds);
        Assert.Equal(FeatureCatalog.Design, Assert.Single(items, x => x.Id == "collaboration.jobs").FeatureId);

        // 清空可見群組＝所有登入者可見。
        (await admin.PutAsJsonAsync($"{Items}/{link.Id}", new NavigationItemUpdateRequest("link", null, "人資公告", $"https://{LinkHost}/hr", false, null, []))).EnsureSuccessStatusCode();
        Assert.Empty(Assert.Single(await ListAsync(admin), x => x.Id == link.Id).AudienceGroupIds);
        Assert.Contains((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.navigation_item&result=saved"))!, x => x.DetailsJson!.Contains("\"audienceGroupIds\":[\"administrators\"]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task KindChangesUnknownNodesDuplicatesAndDisallowedHostsAreRejected()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        var link = await CreateAsync(admin, Link("公告", $"https://{LinkHost}/a"));
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Items}/{BuiltInAccess.WorkNavigation}", new NavigationItemUpdateRequest("link", null, "工作", $"https://{LinkHost}/", true, null, null)), HttpStatusCode.BadRequest, "validation_failed");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Items}/missing", new NavigationItemUpdateRequest("group", null, "不存在", null, true, null, null)), HttpStatusCode.NotFound, "navigation_item_not_found");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Items}/work.projects", new NavigationItemUpdateRequest("page", FeatureCatalog.Chat, null, null, true, null, null)), HttpStatusCode.Conflict, "navigation_page_duplicate");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Items}/{link.Id}", new NavigationItemUpdateRequest("link", null, "公告", "https://evil.example.net/", true, null, null)), HttpStatusCode.BadRequest, "navigation_link_host_not_allowed");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Items}/{link.Id}", new NavigationItemUpdateRequest("link", null, "公告", $"https://{LinkHost}/", true, null, ["missing"])), HttpStatusCode.BadRequest, "unknown_access_id");

        var items = await ListAsync(admin);
        Assert.Equal("group", Assert.Single(items, x => x.Id == BuiltInAccess.WorkNavigation).Kind);
        Assert.Equal(FeatureCatalog.Projects, Assert.Single(items, x => x.Id == "work.projects").FeatureId);
        Assert.Equal($"https://{LinkHost}/a", Assert.Single(items, x => x.Id == link.Id).Url);
    }
}
