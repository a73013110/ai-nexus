using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Identity.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiNexus.Features.Work.Projects.Management;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using AiNexus.Features.Admin.AccessControl.Management;

namespace AiNexus.IntegrationTests.Identity;

public sealed class TestIdentityTests
{
    [Fact]
    public async Task TestingUsesTargetPermissionsOwnershipAuditAndReturnsWithoutTargetPassword()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice");
        var id = await CreateUser(admin, LocalAccount()); var own = await ChatApi.CreateConversation(admin);
        await Begin(admin, id);
        var me = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal(id, me.Id); Assert.DoesNotContain(me.Access.Features, x => x.Id == "identity.users");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/conversations/{own.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/usage")).StatusCode);
        (await admin.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("測試身分專案"))).EnsureSuccessStatusCode();
        (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).EnsureSuccessStatusCode();
        await Csrf(admin); Assert.Equal(actor.Id, (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit"))!;
        Assert.Contains(audit, x => x.Action == "projects.project.created" && x.Actor == "alice" && x.ActingAs == "tester");
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditEvents.SingleAsync(x => x.Action == "projects.project.created");
        Assert.Equal(actor.Id, entry.ActorId); Assert.Equal(id, entry.OwnerId);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "identity.test_identity.started" && x.Result == "completed"));
        Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "identity.test_identity.ended" && x.Result == "completed"));
    }

    [Fact]
    public async Task TestingAnotherAdministratorWritesManagementUnderTheTargetButCannotNest()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice");
        var id = await CreateUser(admin, LocalAccount(roles: ["member", "administrator"]));
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(id, "短")), HttpStatusCode.BadRequest, "test_reason_required");
        await Begin(admin, id);
        // 管理 API 與業務 API 一樣依目標的權限執行；稽核以 ActorId 留下實際操作的管理者。
        (await admin.PutAsJsonAsync("/api/v1/admin/features/chat", new FeatureUpdateRequest("測試身分改名", true))).EnsureSuccessStatusCode();
        // 已在測試身分時先以專屬代碼拒絕巢狀切換，不檢查測試目的。
        await AssertCodeAsync(await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(Guid.NewGuid(), "")), HttpStatusCode.Conflict, "test_identity_nested");
        (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).EnsureSuccessStatusCode();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditEvents.SingleAsync(x => x.Action == "access-control.feature.updated" && x.Result == "completed");
        Assert.Equal(actor.Id, entry.ActorId); Assert.Equal(id, entry.OwnerId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExpiryOrTargetRevocationRestoresOnlyTheStillAuthorizedSource(bool expired)
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var me = await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = await db.Users.SingleAsync(x => x.Id == me.Id); var target = await db.Users.SingleAsync(x => x.Id == id);
        var principal = SessionIdentity.Principal(target, "test", actor, "ad", DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 15));
        if (!expired) { target.SecurityVersion++; await db.SaveChangesAsync(); }
        using var test = CookieClient(f, principal);
        var restored = await test.GetAsync("/api/v1/auth/session"); restored.EnsureSuccessStatusCode();
        var session = (await restored.Content.ReadFromJsonAsync<AuthSessionDto>())!;
        Assert.Null(session.Testing); Assert.Equal(actor.Id, session.UserId);
        Assert.Contains("AiNexus.Session", restored.Headers.GetValues("Set-Cookie").Select(x => x.Split('=')[0]));
        test.DefaultRequestHeaders.Remove("Cookie");
        test.DefaultRequestHeaders.Add("Cookie", restored.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("AiNexus.Session=", StringComparison.Ordinal)).Split(';')[0]);
        Assert.Equal(actor.Id, (await test.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id);
        var ended = await db.AuditEvents.SingleAsync(x => x.Action == "identity.test_identity.ended" && x.Result == "completed");
        Assert.Contains($"\"reason\":\"{(expired ? "expired" : "target_changed")}\"", ended.DetailsJson);
    }

    [Fact]
    public async Task ARequestCrossingTestExpiryCannotWriteUnderTheRestoredAdministrator()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var me = await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = await db.Users.SingleAsync(x => x.Id == me.Id); var target = await db.Users.SingleAsync(x => x.Id == id);
        using var expired = CookieClient(f, SessionIdentity.Principal(target, "test", actor, "ad", DateTimeOffset.UtcNow.AddMinutes(-1)));
        var response = await expired.PostAsJsonAsync("/api/v1/conversations", new { title = "此操作不可遷移至管理者" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(actor.Id + ":", response.Headers.GetValues("X-Active-Identity").Single());
        Assert.Equal(0, await db.Conversations.CountAsync());
    }

    [Fact]
    public async Task AReturnedTestCookieCannotBeReplayedToContinueUsingTheTarget()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        var cookie = await Begin(admin, id);
        (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).EnsureSuccessStatusCode();
        using var replay = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Conflict, (await replay.GetAsync("/api/v1/me")).StatusCode);
        using var scope = f.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditEvents.CountAsync(x => x.Action == "identity.test_identity.ended"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LogoutOrReloginRevokesThePreviousTestCookie(bool logout)
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        var cookie = await Begin(admin, id);
        if (logout) (await admin.PostAsync("/api/v1/auth/logout", null)).EnsureSuccessStatusCode();
        else await Login(admin, "alice");
        using var replay = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Conflict, (await replay.GetAsync("/api/v1/me")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ended = await db.AuditEvents.SingleAsync(x => x.Action == "identity.test_identity.ended");
        Assert.Equal("completed", ended.Result);
        Assert.Contains(logout ? "signed_out" : "signed_in", ended.DetailsJson);
    }

    [Fact]
    public async Task RevokingSourceAdminInvalidatesTestInsteadOfRestoringPrivileges()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var actor = await Login(admin, "alice"); var id = await CreateUser(admin, LocalAccount());
        await Begin(admin, id);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Remove(await db.Set<UserRole>().SingleAsync(x => x.UserId == actor.Id && x.RoleId == "administrator")); await db.SaveChangesAsync();
        }
        Assert.False((await admin.GetFromJsonAsync<AuthSessionDto>("/api/v1/auth/session"))!.Authenticated);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsync("/api/v1/auth/test-identity/end", null)).StatusCode);
    }

    private static async Task<string> Begin(HttpClient admin, Guid target)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(target, "驗證角色與資源權限")); response.EnsureSuccessStatusCode();
        await Csrf(admin);
        return response.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("AiNexus.Session=", StringComparison.Ordinal)).Split(';')[0];
    }
}
