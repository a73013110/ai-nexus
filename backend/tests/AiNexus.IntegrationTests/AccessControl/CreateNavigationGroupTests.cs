using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Audit;
using AiNexus.Features.AccessControl.Navigation;
using AiNexus.Features.Operations;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class CreateNavigationGroupTests
{
    [Fact]
    public async Task AdministratorCreatesNestedGroupThatTheCatalogListsAndTheAuditRecords()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("reports", " 報表 ", "system", 40))).EnsureSuccessStatusCode();
        var catalog = (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!;
        var group = Assert.Single(catalog.NavigationGroups, x => x.Id == "reports");
        Assert.Equal(("報表", "system", 40), (group.Name, group.ParentId, group.SortOrder));
        Assert.Contains(catalog.NavigationGroups, x => x.Id == "work" && x.ParentId == null);
        Assert.Contains((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.navigation_group"))!, x => x.Result == "saved" && x.DetailsJson!.Contains("reports", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateIdUnknownParentAndBadFormatAreRejectedAndAudited()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("work", "重複", null, 10)), HttpStatusCode.Conflict, "navigation_group_exists");
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("orphan", "孤兒", "missing", 10)), HttpStatusCode.NotFound, "navigation_group_not_found");
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("Bad Id", "格式", null, 10)), HttpStatusCode.BadRequest, "invalid_access_id");
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("late", "順序", null, 10001)), HttpStatusCode.BadRequest, "invalid_order");
        Assert.DoesNotContain((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.NavigationGroups, x => x.Id is "orphan" or "late");
        Assert.Equal(4, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.navigation_group&result=failed"))!.Length);
    }

    [Fact]
    public async Task MembersCannotCreateGroups()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await member.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("mine", "我的", null, 10)), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    internal static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
