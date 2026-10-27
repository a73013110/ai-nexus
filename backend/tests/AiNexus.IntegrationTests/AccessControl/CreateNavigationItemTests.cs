using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Audit;
using static AiNexus.IntegrationTests.Support.NavigationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class CreateNavigationItemTests
{
    [Fact]
    public async Task AdministratorCreatesGroupPageAndLinkAtTheEndOfTheirLevel()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        var before = await ListAsync(admin);
        var group = await CreateAsync(admin, Group(" 報表 ", BuiltInAccess.SystemNavigation));
        Assert.Matches("^[0-9a-f]{32}$", group.Id);
        Assert.Equal(("group", "報表", BuiltInAccess.SystemNavigation), (group.Kind, group.Name, group.ParentId));
        Assert.Equal(before.Where(x => x.ParentId == BuiltInAccess.SystemNavigation).Max(x => x.SortOrder) + 10, group.SortOrder);

        var page = await CreateAsync(admin, Page(FeatureCatalog.Jobs, group.Id));
        var link = await CreateAsync(admin, Link("公告", $"https://{LinkHost}/news?tab=1", group.Id, BuiltInAccess.WorkspaceGroup) with { OpenInNewTab = false, Icon = "globe" });
        Assert.Equal((10, 20), (page.SortOrder, link.SortOrder));
        Assert.Equal((null, null, true), (page.Name, page.Icon, page.OpenInNewTab));
        Assert.Equal((false, "globe"), (link.OpenInNewTab, link.Icon));
        Assert.Equal([BuiltInAccess.WorkspaceGroup], link.AudienceGroupIds);

        var listed = await ListAsync(admin);
        Assert.Equal(before.Length + 3, listed.Length);
        Assert.Contains(listed, x => x.Id == link.Id && x.Url == $"https://{LinkHost}/news?tab=1" && x.AudienceGroupIds.SequenceEqual([BuiltInAccess.WorkspaceGroup]));
        Assert.Equal(3, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.navigation_item.created&result=success"))!.Length);
    }

    [Fact]
    public async Task RequestShapesThatDoNotFitTheKindAreRejectedBeforeTheHandler()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        NavigationItemCreateRequest[] invalid =
        [
            new("folder", null, null, "未知", null, true, null, null),
            Page(FeatureCatalog.Jobs) with { Name = "改名" },
            Page(FeatureCatalog.Jobs) with { Icon = "globe" },
            Group("分群") with { FeatureId = FeatureCatalog.Jobs },
            Group("分群") with { AudienceGroupIds = [BuiltInAccess.WorkspaceGroup] },
            Group(" "),
            Link("沒有網址", ""),
            Link("非 https", $"http://{LinkHost}/"),
            Link("含帳密", $"https://user:secret@{LinkHost}/"),
            Link("相對網址", "/work/chat"),
        ];
        foreach (var request in invalid)
            await AssertCodeAsync(await admin.PostAsJsonAsync(Items, request), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task RulesThatNeedTheDatabaseOrSettingsAreRejectedAndAudited()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        var before = (await ListAsync(admin)).Length;
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Link("外站", "https://evil.example.net/")), HttpStatusCode.BadRequest, "navigation_link_host_not_allowed");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Link("子網域", $"https://sub.{LinkHost}/")), HttpStatusCode.BadRequest, "navigation_link_host_not_allowed");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Link("群組", $"https://{LinkHost}/", null, "missing")), HttpStatusCode.BadRequest, "unknown_access_id");
        // 動作與閱讀器不是頁面，沒有網址可以放進選單。
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Page(FeatureCatalog.DiagnosticsLogsExport)), HttpStatusCode.BadRequest, "unknown_access_id");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Page(FeatureCatalog.FilesReader)), HttpStatusCode.BadRequest, "unknown_access_id");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Page(FeatureCatalog.Jobs, "missing")), HttpStatusCode.BadRequest, "navigation_parent_invalid");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Page(FeatureCatalog.Jobs, FeatureCatalog.Dashboard)), HttpStatusCode.BadRequest, "navigation_parent_invalid");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Page(FeatureCatalog.Chat, BuiltInAccess.WorkNavigation)), HttpStatusCode.Conflict, "navigation_page_duplicate");
        Assert.Equal(before, (await ListAsync(admin)).Length);
        Assert.Equal(8, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.navigation_item.created&result=failed"))!.Length);
        // 同一頁面放在不同層可以。
        await CreateAsync(admin, Page(FeatureCatalog.Chat));
    }

    [Fact]
    public async Task TheMenuIsAtMostFourLevelsDeep()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        // system（1）→ system.access（2）→ 第 3 層 → 第 4 層；第 4 層底下不能再放任何東西。
        var third = await CreateAsync(admin, Group("第三層", BuiltInAccess.AccessNavigation));
        await CreateAsync(admin, Page(FeatureCatalog.Jobs, third.Id));
        var fourth = await CreateAsync(admin, Group("第四層", third.Id));
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Page(FeatureCatalog.Jobs, fourth.Id)), HttpStatusCode.BadRequest, "navigation_depth_exceeded");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Group("第五層", fourth.Id)), HttpStatusCode.BadRequest, "navigation_depth_exceeded");
    }

    [Fact]
    public async Task WithoutAnAllowListNoExternalLinkCanBeSaved()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PostAsJsonAsync(Items, Link("公告", $"https://{LinkHost}/")), HttpStatusCode.BadRequest, "navigation_link_host_not_allowed");
    }

    [Fact]
    public async Task MembersCannotReadOrManageTheMenu()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await member.GetAsync(Items), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PostAsJsonAsync(Items, Group("我的")), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
