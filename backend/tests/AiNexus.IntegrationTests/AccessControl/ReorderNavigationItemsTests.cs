using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Audit;
using static AiNexus.IntegrationTests.Support.NavigationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class ReorderNavigationItemsTests
{
    private const string Order = Items + "/order";

    [Fact]
    public async Task AdministratorMovesAndReordersSeveralNodesAtOnce()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync(Order, new NavigationOrderRequest(
        [
            new("collaboration.jobs", BuiltInAccess.WorkNavigation, 5),
            new("work.chat", BuiltInAccess.WorkNavigation, 60),
            new(BuiltInAccess.AccessNavigation, null, 50),
        ]))).EnsureSuccessStatusCode();
        var items = (await ListAsync(admin)).ToDictionary(x => x.Id);
        Assert.Equal((BuiltInAccess.WorkNavigation, 5), (items["collaboration.jobs"].ParentId, items["collaboration.jobs"].SortOrder));
        Assert.Equal(60, items["work.chat"].SortOrder);
        Assert.Equal(((string?)null, 50), (items[BuiltInAccess.AccessNavigation].ParentId, items[BuiltInAccess.AccessNavigation].SortOrder));
        // 沒列到的節點不動，子節點跟著父節點一起移動。
        Assert.Equal(BuiltInAccess.AccessNavigation, items["system.access.identity-users"].ParentId);
        Assert.Equal(20, items["work.projects"].SortOrder);
        Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.navigation_item.reordered&result=success"))!);
    }

    [Fact]
    public async Task OneInvalidMoveRejectsTheWholeBatch()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var deeper = await CreateAsync(admin, Group("更多", BuiltInAccess.WorkNavigation));
        await CreateAsync(admin, Page(FeatureCatalog.Jobs, BuiltInAccess.WorkNavigation));
        var valid = new NavigationOrderItem("work.chat", BuiltInAccess.WorkNavigation, 99);
        (NavigationOrderItem Move, HttpStatusCode Status, string Code)[] cases =
        [
            (new(BuiltInAccess.SystemNavigation, BuiltInAccess.AccessNavigation, 10), HttpStatusCode.BadRequest, "navigation_item_cycle"),
            (new(BuiltInAccess.SystemNavigation, BuiltInAccess.SystemNavigation, 10), HttpStatusCode.BadRequest, "navigation_item_cycle"),
            // system（含 system.access 與其頁面，高 3 層）放到第 2 層的分群底下會變成 5 層。
            (new(BuiltInAccess.SystemNavigation, deeper.Id, 10), HttpStatusCode.BadRequest, "navigation_depth_exceeded"),
            (new("work.projects", FeatureCatalog.Dashboard, 10), HttpStatusCode.BadRequest, "navigation_parent_invalid"),
            (new("work.projects", "missing", 10), HttpStatusCode.BadRequest, "navigation_parent_invalid"),
            (new("collaboration.jobs", BuiltInAccess.WorkNavigation, 10), HttpStatusCode.Conflict, "navigation_page_duplicate"),
            (new("missing", null, 10), HttpStatusCode.NotFound, "navigation_item_not_found"),
        ];
        foreach (var (move, status, code) in cases)
            await AssertCodeAsync(await admin.PutAsJsonAsync(Order, new NavigationOrderRequest([valid, move])), status, code);

        var items = (await ListAsync(admin)).ToDictionary(x => x.Id);
        Assert.Equal(10, items["work.chat"].SortOrder);
        Assert.Null(items[BuiltInAccess.SystemNavigation].ParentId);
        Assert.Equal(BuiltInAccess.CollaborationNavigation, items["collaboration.jobs"].ParentId);
        Assert.Equal(cases.Length, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.navigation_item.reordered&result=failed"))!.Length);
    }

    [Fact]
    public async Task MalformedBatchesAreInvalidOrders()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PutAsJsonAsync(Order, new NavigationOrderRequest([new("work.chat", BuiltInAccess.WorkNavigation, 10001)])), HttpStatusCode.BadRequest, "invalid_order");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Order, new NavigationOrderRequest([new("work.chat", null, -1)])), HttpStatusCode.BadRequest, "invalid_order");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Order, new NavigationOrderRequest([new("work.chat", null, 10), new("work.chat", null, 20)])), HttpStatusCode.BadRequest, "invalid_order");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Order, new NavigationOrderRequest([])), HttpStatusCode.BadRequest, "invalid_order");
    }

    [Fact]
    public async Task MembersCannotReorderTheMenu()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await member.PutAsJsonAsync(Order, new NavigationOrderRequest([new("work.chat", null, 10)])), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
