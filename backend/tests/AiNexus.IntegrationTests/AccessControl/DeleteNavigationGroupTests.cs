using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Audit;
using static AiNexus.IntegrationTests.AccessControl.CreateNavigationGroupTests;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Operations;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class DeleteNavigationGroupTests
{
    [Fact]
    public async Task GroupsWithSubgroupsOrFeaturesStayUntilEmptied()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("reports", "報表", null, 40))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("monthly", "月報", "reports", 10))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/files", new FeatureUpdateRequest("檔案庫", 20, true, "monthly", "files"))).EnsureSuccessStatusCode();

        await AssertCodeAsync(await admin.DeleteAsync("/api/v1/admin/navigation-groups/reports"), HttpStatusCode.Conflict, "navigation_group_not_empty");
        await AssertCodeAsync(await admin.DeleteAsync("/api/v1/admin/navigation-groups/monthly"), HttpStatusCode.Conflict, "navigation_group_not_empty");
        (await admin.PutAsJsonAsync("/api/v1/admin/features/files", new FeatureUpdateRequest("檔案庫", 20, true, "work", "files"))).EnsureSuccessStatusCode();
        (await admin.DeleteAsync("/api/v1/admin/navigation-groups/monthly")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync("/api/v1/admin/navigation-groups/reports")).EnsureSuccessStatusCode();

        Assert.DoesNotContain((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.NavigationGroups, x => x.Id is "reports" or "monthly");
        Assert.Equal(2, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.navigation_group_delete&result=saved"))!.Length);
        await AssertCodeAsync(await admin.DeleteAsync("/api/v1/admin/navigation-groups/reports"), HttpStatusCode.NotFound, "navigation_group_not_found");
    }

    [Fact]
    public async Task MembersCannotDeleteGroups()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await member.DeleteAsync("/api/v1/admin/navigation-groups/work"), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
