using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Administration.Users;
using AiNexus.Features.Audit;

namespace AiNexus.IntegrationTests.Administration;

public sealed class RoleManagementTests
{
    [Fact]
    public async Task MembersCannotReadAdministrationOrGrantThemselvesRoles()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/admin/users/{me.Id}/roles", new UserRolesRequest(["administrator"]))).StatusCode);
    }

    [Fact]
    public async Task RoleChangesAreAuditedAndPreventSelfLockoutWithoutPartiallySaving()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var actor = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!; var target = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        (await alice.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/roles", new UserRolesRequest(["member", "administrator"]))).EnsureSuccessStatusCode();
        (await bob.GetAsync("/api/v1/admin/catalog")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync($"/api/v1/admin/users/{actor.Id}/roles", new UserRolesRequest(["member"]))).StatusCode);
        Assert.Contains((await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "admin");
        var audit = await alice.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit");
        Assert.Contains(audit!, x => x.Action == "admin.user_roles" && x.ResourceId == target.Id && x.DetailsJson!.Contains("administrator"));
    }

    [Fact]
    public async Task UnknownGrantsAreRejectedAndMissingCsrfCannotMutateAdministration()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/admin/roles/broken", new RoleUpdateRequest("無效", true, ["missing"]))).StatusCode);
        var catalog = (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!; Assert.DoesNotContain(catalog.Roles, x => x.Id == "broken");
        admin.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/api/v1/admin/features/chat", new FeatureUpdateRequest("任意", 10, true))).StatusCode);
    }

    [Fact]
    public async Task FailedMutationsAreRolledBackAuditedAndFiltersKeepAStableCursor()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/analyst", new RoleUpdateRequest("分析人員", true, ["workspace"]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/roles/analyst", new RoleUpdateRequest("資深分析人員", true, ["workspace"]))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/admin/roles/analyst", new RoleUpdateRequest("不可儲存的名稱", true, ["missing"]))).StatusCode);
        var failed = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.role&result=failed"))!);
        Assert.Equal("unknown_access_id", failed.Result);
        var saved = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action=admin.role&result=saved&before={failed.Id}&search=analyst"))!;
        Assert.Equal(2, saved.Length); Assert.True(saved[0].Id > saved[1].Id);
        using var json = JsonDocument.Parse(saved[0].DetailsJson!);
        Assert.Equal("分析人員", json.RootElement.GetProperty("before").GetProperty("name").GetString());
        Assert.Equal("資深分析人員", json.RootElement.GetProperty("after").GetProperty("name").GetString());
        Assert.Equal("資深分析人員", (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Roles.Single(x => x.Id == "analyst").Name);
        var cursor = (await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action=admin.role&before={saved[0].Id}&result=saved"))!;
        Assert.Equal(saved[1].Id, Assert.Single(cursor).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/audit?from=2026-10-05&until=2026-10-01")).StatusCode);
    }
}
