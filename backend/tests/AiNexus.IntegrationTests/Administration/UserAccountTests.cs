using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Administration;

public sealed class UserAccountTests
{
    [Fact]
    public async Task ManualLocalCrudHashesPasswordAndRevokesSessionsWithoutDeletingContent()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var id = await CreateUser(admin, LocalAccount());
        using var local = f.CreateClient(); var me = await Login(local, "TeStEr", "local");
        Assert.Equal(id, me.Id); Assert.Equal("手動使用者", me.DisplayName);
        var conversation = await ChatApi.CreateConversation(local);
        var json = await admin.GetStringAsync("/api/v1/admin/users");
        Assert.Contains("hasLocalPassword", json); Assert.DoesNotContain(Password, json); Assert.DoesNotContain("argon2id", json);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            Assert.StartsWith($"$argon2id$v=19$m={NexusFactory.PasswordCost.MemoryKiB},t={NexusFactory.PasswordCost.Iterations},p=1$", (await db.Users.SingleAsync(x => x.Id == id)).PasswordHash);
            Assert.DoesNotContain(Password, (await db.AuditEvents.SingleAsync(x => x.Action == "admin.user")).DetailsJson);
        }
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", LocalAccount() with { DisplayName = "更新姓名", Password = "a replacement password 99!" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.GetAsync("/api/v1/me")).StatusCode);
        (await admin.DeleteAsync($"/api/v1/admin/users/{id}")).EnsureSuccessStatusCode();
        using var check = f.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<NexusDbContext>();
        var deleted = await database.Users.SingleAsync(x => x.Id == id);
        Assert.NotNull(deleted.DeletedAt); Assert.Null(deleted.PasswordHash);
        Assert.True(await database.Conversations.AnyAsync(x => x.Id == conversation.Id));
        Assert.DoesNotContain(id.ToString(), await admin.GetStringAsync("/api/v1/admin/users"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/users", LocalAccount())).StatusCode);
    }

    [Fact]
    public async Task PreProvisionedUserCanUseAdAndLocalOnTheSameIdentityAndAdCanBeRevoked()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var request = LocalAccount("local-bob") with { AdEnabled = true, AdAccount = "bob" };
        var id = await CreateUser(admin, request);
        using var ad = f.CreateClient(); using var local = f.CreateClient();
        Assert.Equal(id, (await Login(ad, "bob")).Id);
        Assert.Equal(id, (await Login(local, "local-bob", "local")).Id);
        Assert.Equal("手動使用者", (await ad.GetFromJsonAsync<MeDto>("/api/v1/me"))!.DisplayName);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", request with { AdEnabled = false, Password = null })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await ad.GetAsync("/api/v1/me")).StatusCode);
        using var again = f.CreateClient(); await Csrf(again);
        Assert.Equal(HttpStatusCode.Forbidden, (await again.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("bob", "fixture-password"))).StatusCode);
        using var scope = f.Services.CreateScope(); Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.CountAsync());
        using var freshLocal = f.CreateClient(); Assert.Equal(id, (await Login(freshLocal, "local-bob", "local")).Id);
    }

    [Fact]
    public async Task AccountPolicyRejectsMissingPasswordInvalidRoleSelfDisableAndMissingCsrf()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/users", LocalAccount() with { Password = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/users", LocalAccount() with { RoleIds = ["missing"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{actor.Id}", LocalAccount("alice") with { Enabled = false, AdEnabled = true, AdAccount = "alice", RoleIds = ["administrator"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/users/{actor.Id}")).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/admin/users", LocalAccount())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(Guid.NewGuid(), "檢查權限"))).StatusCode);
    }
}
