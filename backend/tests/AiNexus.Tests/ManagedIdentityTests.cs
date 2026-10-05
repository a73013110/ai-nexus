using System.Net;
using System.Net.Http.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Administration;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Projects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class ManagedIdentityTests
{
    private const string Password = "fixture long password 42!";
    private static UserAccountRequest Account(string name = "tester", string[]? roles = null) =>
        new("手動使用者", true, false, true, null, name, roles ?? ["member"], Password);

    [Fact]
    public async Task ManualLocalCrudHashesPasswordAndRevokesSessionsWithoutDeletingContent()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var id = await Create(admin, Account());
        using var local = f.CreateClient(); var me = await Login(local, "TeStEr", "local");
        Assert.Equal(id, me.Id); Assert.Equal("手動使用者", me.DisplayName);
        var conversation = await ChatApiTests.CreateConversation(local);
        var json = await admin.GetStringAsync("/api/v1/admin/users");
        Assert.Contains("hasLocalPassword", json); Assert.DoesNotContain(Password, json); Assert.DoesNotContain("argon2id", json);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=1$", (await db.Users.SingleAsync(x => x.Id == id)).PasswordHash);
            Assert.DoesNotContain(Password, (await db.AuditEvents.SingleAsync(x => x.Action == "admin.user")).DetailsJson);
        }
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", Account() with { DisplayName = "更新姓名", Password = "a replacement password 99!" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.GetAsync("/api/v1/me")).StatusCode);
        (await admin.DeleteAsync($"/api/v1/admin/users/{id}")).EnsureSuccessStatusCode();
        using var check = f.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<NexusDbContext>();
        var deleted = await database.Users.SingleAsync(x => x.Id == id);
        Assert.NotNull(deleted.DeletedAt); Assert.Null(deleted.PasswordHash);
        Assert.True(await database.Conversations.AnyAsync(x => x.Id == conversation.Id));
        Assert.DoesNotContain(id.ToString(), await admin.GetStringAsync("/api/v1/admin/users"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/users", Account())).StatusCode);
    }

    [Fact]
    public async Task PreProvisionedUserCanUseAdAndLocalOnTheSameIdentityAndAdCanBeRevoked()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var request = Account("local-bob") with { AdEnabled = true, AdAccount = "bob" };
        var id = await Create(admin, request);
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
    public async Task LocalNameMatchingBootstrapDoesNotGrantAdministration()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice", "tester"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); await Create(admin, Account());
        using var local = f.CreateClient(); var me = await Login(local, "tester", "local");
        Assert.DoesNotContain(me.Access.Features, x => x.Id == "admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await local.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(Guid.NewGuid(), "檢查權限"))).StatusCode);
    }

    [Fact]
    public async Task LoginLocksAfterRepeatedFailuresAndPasswordResetClearsTheLock()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await Create(admin, Account());
        using var local = f.CreateClient(); await Csrf(local);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await local.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("tester", "wrong password!", "local"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("tester", Password, "local"))).StatusCode);
        using (var scope = f.Services.CreateScope()) Assert.True((await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.SingleAsync(x => x.Id == id)).LockedUntil > DateTimeOffset.UtcNow);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}", Account())).EnsureSuccessStatusCode();
        Assert.Equal(id, (await Login(local, "tester", "local")).Id);
    }

    [Fact]
    public async Task AccountPolicyRejectsMissingPasswordInvalidRoleSelfDisableAndMissingCsrf()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/users", Account() with { Password = null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/users", Account() with { RoleIds = ["missing"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{actor.Id}", Account("alice") with { Enabled = false, AdEnabled = true, AdAccount = "alice", RoleIds = ["administrator"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/users/{actor.Id}")).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/admin/users", Account())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(Guid.NewGuid(), "檢查權限"))).StatusCode);
    }

    [Fact]
    public async Task TestingUsesTargetPermissionsOwnershipAuditAndReturnsWithoutTargetPassword()
    {
        await using var f = new NexusFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice");
        var id = await Create(admin, Account()); var own = await ChatApiTests.CreateConversation(admin);
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
        var id = await Create(admin, Account(roles: ["member", "administrator"])); await Begin(admin, id);
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
        using var admin = f.CreateClient(); var me = await Login(admin, "alice"); var id = await Create(admin, Account());
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
        using var admin = f.CreateClient(); var me = await Login(admin, "alice"); var id = await Create(admin, Account());
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
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await Create(admin, Account());
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
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await Create(admin, Account());
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
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice"); var id = await Create(admin, Account());
        await Begin(admin, id);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == actor.Id && x.RoleId == "administrator")); await db.SaveChangesAsync();
        }
        Assert.False((await admin.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).StatusCode);
    }

    [Fact]
    public async Task WindowsLoginRequiresWindowsIdentityEvenWhenALocalCookieExists()
    {
        await using var f = new NexusFactory(administrators: ["alice"], services: services =>
            services.PostConfigure<AuthenticationOptions>(options => { options.DefaultAuthenticateScheme = "NexusSession"; options.DefaultChallengeScheme = "NexusSession"; }));
        using var admin = await f.SignedInAsync(); await Create(admin, Account());
        using var local = f.CreateClient(); await Login(local, "tester", "local");
        Assert.Equal(HttpStatusCode.Unauthorized, (await local.GetAsync("/api/v1/auth/windows")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await local.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task LegacyAdCookiesKeepTheirLoginMethodAndAreRevokedByPolicyChanges()
    {
        await using var f = new NexusFactory(ldap: true);
        using var modern = f.CreateClient(); var me = await Login(modern, "alice");
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == me.Id);
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new(System.Security.Claims.ClaimTypes.PrimarySid, user.Sid), new(System.Security.Claims.ClaimTypes.Name, user.Account)
        ], AuthEndpoints.CookieScheme));
        using var legacy = CookieClient(f, principal);
        var session = (await legacy.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        Assert.True(session.Authenticated); Assert.Equal("ad", session.Method);
        user.AdEnabled = false; user.SecurityVersion++; await db.SaveChangesAsync();
        Assert.False((await legacy.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
    }

    [Fact]
    public async Task Argon2UsesRandomSaltRejectsWrongPasswordAndBoundsUntrustedParameters()
    {
        using var hash = new Argon2Passwords();
        var first = await hash.HashAsync(Password, default); var second = await hash.HashAsync(Password, default);
        Assert.NotEqual(first, second); Assert.True((await hash.VerifyAsync(Password, first, default)).Valid);
        Assert.False((await hash.VerifyAsync("wrong", first, default)).Valid);
        Assert.False((await hash.VerifyAsync(Password, first.Replace("m=65536", "m=999999999"), default)).Valid);
        Assert.Throws<ApiException>(() => Argon2Passwords.Validate("short"));
    }

    [Fact]
    public void EveryMappedTableAndColumnHasADescription()
    {
        using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer("Server=fixture;Database=AiNexus;Integrated Security=true").Options);
        var model = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
        Assert.All(model.GetEntityTypes(), entity => {
            Assert.False(string.IsNullOrWhiteSpace(entity.GetComment()));
            Assert.All(entity.GetProperties(), property => Assert.False(string.IsNullOrWhiteSpace(property.GetComment())));
        });
    }

    private static HttpClient CookieClient(NexusFactory f, System.Security.Claims.ClaimsPrincipal principal)
    {
        var options = f.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(AuthEndpoints.CookieScheme);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, AuthEndpoints.CookieScheme);
        var client = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "Nexus.Session=" + options.TicketDataFormat.Protect(ticket)); return client;
    }
    private static async Task<Guid> Create(HttpClient admin, UserAccountRequest request)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedUserDto>())!.Id;
    }
    private static async Task<string> Begin(HttpClient admin, Guid target)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(target, "驗證角色與資源權限")); response.EnsureSuccessStatusCode();
        await Csrf(admin);
        return response.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("Nexus.Session=", StringComparison.Ordinal)).Split(';')[0];
    }
    private static async Task Csrf(HttpClient client)
    {
        var session = (await client.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!;
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF"); client.DefaultRequestHeaders.Add("X-Nexus-CSRF", session.CsrfToken);
    }
    private static async Task<MeDto> Login(HttpClient client, string account, string method = "ad")
    {
        await Csrf(client); (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest(account, method == "local" ? Password : "fixture-password", method))).EnsureSuccessStatusCode();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF"); client.DefaultRequestHeaders.Add("X-Nexus-CSRF", me.CsrfToken); return me;
    }
}
