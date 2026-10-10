using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Audit;
using AiNexus.Features.Identity.Sessions;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Identity;

public sealed class TestIdentityTests
{
    [Fact]
    public async Task TestingUsesTargetPermissionsOwnershipAuditAndReturnsWithoutTargetPassword()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice");
        var id = await CreateUser(admin, LocalAccount()); var own = await ChatApi.CreateConversation(admin);
        await Begin(admin, id);
        var me = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal(id, me.Id); Assert.DoesNotContain(me.Access.Features, x => x.Id == "admin");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/conversations/{own.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/catalog")).StatusCode);
        (await admin.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("測試身分專案"))).EnsureSuccessStatusCode();
        (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).EnsureSuccessStatusCode();
        await Csrf(admin); Assert.Equal(actor.Id, (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit"))!;
        Assert.Contains(audit, x => x.Action == "project.created" && x.Actor == "alice@fixture.test" && x.ActingAs == "TESTER");
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var entry = await db.AuditEvents.SingleAsync(x => x.Action == "project.created");
        Assert.Equal(actor.Id, entry.ActorId); Assert.Equal(id, entry.OwnerId);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "identity.test_start"));
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "identity.test_end"));
    }

    [Fact]
    public async Task TestingAnotherAdministratorCannotNestOrWriteManagement()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var id = await CreateUser(admin, LocalAccount(roles: ["member", "administrator"])); await Begin(admin, id);
        (await admin.GetAsync("/api/v1/admin/catalog")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/api/v1/admin/features/chat", new FeatureUpdateRequest("越權", 10, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(Guid.NewGuid(), "巢狀切換"))).StatusCode);
        (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiryOrTargetRevocationRestoresOnlyTheStillAuthorizedSource(bool expired)
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var me = await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var actor = await db.Users.SingleAsync(x => x.Id == me.Id); var target = await db.Users.SingleAsync(x => x.Id == id);
        var principal = SessionIdentity.Principal(target, "test", actor, "ad", DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 15));
        if (!expired) { target.SecurityVersion++; await db.SaveChangesAsync(); }
        using var test = CookieClient(f, principal);
        var restored = await test.GetAsync("/api/v1/auth/session"); restored.EnsureSuccessStatusCode();
        var session = (await restored.Content.ReadFromJsonAsync<AuthSessionDto>())!;
        Assert.Null(session.Testing); Assert.Equal(actor.Id, session.UserId);
        Assert.Contains("Nexus.Session", restored.Headers.GetValues("Set-Cookie").Select(x => x.Split('=')[0]));
        test.DefaultRequestHeaders.Remove("Cookie");
        test.DefaultRequestHeaders.Add("Cookie", restored.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("Nexus.Session=", StringComparison.Ordinal)).Split(';')[0]);
        Assert.Equal(actor.Id, (await test.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "identity.test_end" && x.Result == "restored"));
    }

    [Fact]
    public async Task ARequestCrossingTestExpiryCannotWriteUnderTheRestoredAdministrator()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var me = await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var actor = await db.Users.SingleAsync(x => x.Id == me.Id); var target = await db.Users.SingleAsync(x => x.Id == id);
        using var expired = CookieClient(f, SessionIdentity.Principal(target, "test", actor, "ad", DateTimeOffset.UtcNow.AddMinutes(-1)));
        var response = await expired.PostAsJsonAsync("/api/v1/conversations", new { title = "此操作不可遷移至管理者" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(actor.Id + ":", response.Headers.GetValues("X-Nexus-Identity").Single());
        Assert.Equal(0, await db.Conversations.CountAsync());
    }

    [Fact]
    public async Task AReturnedTestCookieCannotBeReplayedToContinueUsingTheTarget()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        var cookie = await Begin(admin, id);
        (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).EnsureSuccessStatusCode();
        using var replay = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Conflict, (await replay.GetAsync("/api/v1/me")).StatusCode);
        using var scope = f.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<NexusDbContext>().AuditEvents.CountAsync(x => x.Action == "identity.test_end"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LogoutOrReloginRevokesThePreviousTestCookie(bool logout)
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        var cookie = await Begin(admin, id);
        if (logout) (await admin.PostAsync("/api/v1/auth/logout", null)).EnsureSuccessStatusCode();
        else await Login(admin, "alice");
        using var replay = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Conflict, (await replay.GetAsync("/api/v1/me")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var ended = await db.AuditEvents.SingleAsync(x => x.Action == "identity.test_end");
        Assert.Equal("revoked", ended.Result);
        Assert.Contains(logout ? "signed_out" : "signed_in", ended.DetailsJson);
    }

    [Fact]
    public async Task RevokingSourceAdminInvalidatesTestInsteadOfRestoringPrivileges()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        await Begin(admin, id);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == actor.Id && x.RoleId == "administrator")); await db.SaveChangesAsync();
        }
        Assert.False((await admin.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).StatusCode);
    }

    private static async Task<string> Begin(HttpClient admin, Guid target)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(target, "驗證角色與資源權限")); response.EnsureSuccessStatusCode();
        await Csrf(admin);
        return response.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("Nexus.Session=", StringComparison.Ordinal)).Split(';')[0];
    }
}
