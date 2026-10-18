using System.Net;
using System.Net.Http.Json;
using static AiNexus.IntegrationTests.AccessControl.CreateNavigationGroupTests;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Operations;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class UpdateNavigationGroupTests
{
    [Fact]
    public async Task AdministratorRenamesMovesAndReordersAGroup()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/navigation-groups/collaboration", new NavigationGroupUpdateRequest("團隊協作", "work", 5))).EnsureSuccessStatusCode();
        var group = Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.NavigationGroups, x => x.Id == "collaboration");
        Assert.Equal(("團隊協作", "work", 5), (group.Name, group.ParentId, group.SortOrder));
        (await admin.PutAsJsonAsync("/api/v1/admin/navigation-groups/collaboration", new NavigationGroupUpdateRequest("協作", null, 20))).EnsureSuccessStatusCode();
        Assert.Null(Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.NavigationGroups, x => x.Id == "collaboration").ParentId);
    }

    [Fact]
    public async Task CyclesAndUnknownGroupsAreRejected()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("reports", "報表", "system", 40))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/navigation-groups/system", new NavigationGroupUpdateRequest("系統管理", "reports", 30)), HttpStatusCode.BadRequest, "navigation_group_cycle");
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/navigation-groups/system", new NavigationGroupUpdateRequest("系統管理", "system", 30)), HttpStatusCode.BadRequest, "navigation_group_cycle");
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/navigation-groups/system", new NavigationGroupUpdateRequest("系統管理", "missing", 30)), HttpStatusCode.NotFound, "navigation_group_not_found");
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/navigation-groups/missing", new NavigationGroupUpdateRequest("不存在", null, 30)), HttpStatusCode.NotFound, "navigation_group_not_found");
        Assert.Null(Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.NavigationGroups, x => x.Id == "system").ParentId);
    }

    [Fact]
    public async Task MembersCannotUpdateGroups()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await member.PutAsJsonAsync("/api/v1/admin/navigation-groups/work", new NavigationGroupUpdateRequest("我的", null, 10)), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
