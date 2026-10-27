using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Audit;
using static AiNexus.IntegrationTests.Support.NavigationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class DeleteNavigationItemTests
{
    [Fact]
    public async Task GroupsMustBeEmptiedBeforeDeletionAndLinksTakeTheirAudiencesAlong()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: AllowLinks()); using var admin = await factory.SignedInAsync();
        var group = await CreateAsync(admin, Group("報表"));
        var link = await CreateAsync(admin, Link("公告", $"https://{LinkHost}/", group.Id, BuiltInAccess.WorkspaceGroup, BuiltInAccess.AdministratorsGroup));
        await AssertCodeAsync(await admin.DeleteAsync($"{Items}/{group.Id}"), HttpStatusCode.Conflict, "navigation_item_not_empty");
        await AssertCodeAsync(await admin.DeleteAsync($"{Items}/{BuiltInAccess.SystemNavigation}"), HttpStatusCode.Conflict, "navigation_item_not_empty");

        (await admin.DeleteAsync($"{Items}/{link.Id}")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"{Items}/{group.Id}")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"{Items}/work.chat")).EnsureSuccessStatusCode();
        var items = await ListAsync(admin);
        Assert.DoesNotContain(items, x => x.Id == group.Id || x.Id == link.Id || x.Id == "work.chat");
        Assert.Equal(3, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.navigation_item.deleted&result=success"))!.Length);
        await AssertCodeAsync(await admin.DeleteAsync($"{Items}/{group.Id}"), HttpStatusCode.NotFound, "navigation_item_not_found");
    }

    [Fact]
    public async Task MembersCannotDeleteNodes()
    {
        await using var factory = new AppFactory(); using var member = await factory.SignedInAsync();
        await AssertCodeAsync(await member.DeleteAsync($"{Items}/{BuiltInAccess.WorkNavigation}"), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
