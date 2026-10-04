using System.Net;
using System.Net.Http.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class AdministrationTests
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
    public async Task BootstrapGrantsOnlyConfiguredAccountsAndDoesNotRestoreRevokedPrivileges()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var me = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Contains(me.Access.Features, x => x.Id == "admin"); Assert.DoesNotContain((await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "admin");
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == me.Id && x.RoleId == "administrator")); await db.SaveChangesAsync();
        }
        using var again = await factory.SignedInAsync(); Assert.Equal(HttpStatusCode.Forbidden, (await again.GetAsync("/api/v1/admin/catalog")).StatusCode);
        using var check = factory.Services.CreateScope(); Assert.Equal(1, await check.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.CountAsync(x => x.Action == "admin.bootstrap"));
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
    public async Task DailyQuotaRejectsNewRequestsButKeepsIdempotentReplayAndOtherAccountsIndependent()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("基本工作台", true, ["chat"], new(["test-model"], 1)))).EnsureSuccessStatusCode();
        var conversation = await CreateConversation(bob); var body = new CreateRunRequest(conversation.Id, "test-model", "唯一一次", null, null); var key = Guid.NewGuid().ToString();
        var first = await PostRun(bob, body, key); first.EnsureSuccessStatusCode(); var run = (await first.Content.ReadFromJsonAsync<RunDto>())!; await WaitForTerminal(bob, run.Id);
        var replay = await PostRun(bob, body, key); replay.EnsureSuccessStatusCode(); Assert.Equal(run.Id, (await replay.Content.ReadFromJsonAsync<RunDto>())!.Id);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostRun(bob, new(conversation.Id, "test-model", "超出配額", null, null))).StatusCode);
        var ownPolicy = (await admin.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!; Assert.Equal(0, ownPolicy.RequestsToday);
        var usage = (await admin.GetFromJsonAsync<AdminUsageDto>("/api/v1/admin/usage"))!; Assert.Equal(1, usage.Requests); Assert.Equal(123, usage.InputTokens); Assert.Equal(2, usage.Users);
        var detail = (await bob.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!; Assert.Equal(2, detail.Messages.Count);
    }
    [Fact]
    public async Task ModelDenialAppliesToCatalogContextAndRunAndHiddenNamesStayHidden()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], inference: options => options.ShowModelNames = false);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("基本工作台", true, ["chat"], new([])))).EnsureSuccessStatusCode();
        Assert.Empty((await bob.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "test", "model-1"))).StatusCode);
        var conversation = await CreateConversation(bob);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostRun(bob, new(conversation.Id, "model-1", "test", null, null))).StatusCode);
        Assert.DoesNotContain("test-model", await (await bob.GetAsync("/api/v1/settings/model-policy")).Content.ReadAsStringAsync());
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
}
