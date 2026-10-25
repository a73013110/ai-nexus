using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Admin.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.DirectoryApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class DirectoryBindingTests
{
    private const string Bob = "S-1-5-21-fixture-bob";

    private static string Binding(Guid id) => $"/api/v1/admin/users/{id}/ad-binding";

    [Fact]
    public async Task RebindMovesTheUserToTheNewSidAndKeepsUserIdRolesAndHistory()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        using var bob = f.CreateClient(); var bobId = (await Login(bob, "bob")).Id;
        int version;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<UserRole>().Add(new UserRole { UserId = bobId, RoleId = "administrator" });
            await db.SaveChangesAsync();
            version = (await db.Users.AsNoTracking().SingleAsync(x => x.Id == bobId)).SecurityVersion;
        }

        // bob 換了 AD 帳號：目錄裡的新帳號是 erin.chen（另一個 SID）。
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest(" Erin.Chen "))).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == bobId);
            Assert.Equal((FixtureAdDirectory.Erin, "erin.chen", true), (user.Sid, user.AdAccount, user.AdEnabled));
            Assert.Equal(version + 1, user.SecurityVersion);
            Assert.Equal(["administrator", "member"], await db.Set<UserRole>().Where(x => x.UserId == bobId).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToListAsync());
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "admin.user_ad_bind" && x.ResourceId == bobId && x.Result == "saved"));
        }
        // 以舊 SID 建立的工作階段失效。
        Assert.Equal(HttpStatusCode.Unauthorized, (await bob.GetAsync("/api/v1/me")).StatusCode);
        var row = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users?search=erin"))!.Users.Single();
        Assert.Equal((bobId, true, "erin.chen"), (row.Id, row.Authentication!.DirectoryBound, row.Authentication.AdAccount));
        // 新 SID 已對到原本的使用者：匯入不會另建一位。
        var imported = await Import(admin, FixtureAdDirectory.Erin);
        Assert.Equal(bobId, imported.Single().UserId);
        Assert.NotEqual(DirectoryOutcomes.Create, imported.Single().Outcome);
    }

    [Fact]
    public async Task RebindRejectsUnknownTakenOrSelfTargetsAndRequiresTheUsersGrant()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var alice = await Login(admin, "alice");
        using var member = f.CreateClient(); var bobId = (await Login(member, "bob")).Id;
        await AssertCodeAsync(await member.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest("erin.chen")), HttpStatusCode.Forbidden, "feature_forbidden");

        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest("a b")), HttpStatusCode.BadRequest, "invalid_account");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest("nobody")), HttpStatusCode.NotFound, "directory_person_not_found");
        // SID 已綁定另一位使用者（匯入建立的 carol）。
        await Import(admin, FixtureAdDirectory.Carol);
        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest("carol.lin")), HttpStatusCode.Conflict, "ad_binding_taken");
        // 帳號名已被預建帳號使用：改綁不能搶走別人的 AdAccount。
        await CreateUser(admin, new UserAccountRequest("吳大衛", true, true, false, "dave.wu", null, ["member"]));
        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest("dave.wu")), HttpStatusCode.Conflict, "account_exists");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(alice.Id), new DirectoryBindingRequest("erin.chen")), HttpStatusCode.Conflict, "admin_lockout");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(Guid.NewGuid()), new DirectoryBindingRequest("erin.chen")), HttpStatusCode.NotFound, "user_not_found");
        f.AdDirectory.Unavailable = true;
        await AssertCodeAsync(await admin.PutAsJsonAsync(Binding(bobId), new DirectoryBindingRequest("erin.chen")), HttpStatusCode.ServiceUnavailable, "directory_unavailable");

        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(Bob, (await db.Users.AsNoTracking().SingleAsync(x => x.Id == bobId)).Sid);
    }

    [Fact]
    public async Task UnbindReturnsTheUserToPrebuiltSoTheNextSignInBindsTheSameUser()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var alice = await Login(admin, "alice");
        using var bob = f.CreateClient(); var bobId = (await Login(bob, "bob")).Id;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(Binding(bobId))).StatusCode);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == bobId);
            Assert.Equal((UserAccounts.ManagedSid(bobId), "bob", true), (user.Sid, user.AdAccount, user.AdEnabled));
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "admin.user_ad_unbind" && x.ResourceId == bobId && x.Result == "saved"));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await bob.GetAsync("/api/v1/me")).StatusCode);
        var row = (await admin.GetFromJsonAsync<AdminUsersDto>("/api/v1/admin/users?search=bob"))!.Users.Single();
        Assert.False(row.Authentication!.DirectoryBound);
        await AssertCodeAsync(await admin.DeleteAsync(Binding(bobId)), HttpStatusCode.Conflict, "ad_not_bound");
        await AssertCodeAsync(await admin.DeleteAsync(Binding(alice.Id)), HttpStatusCode.Conflict, "admin_lockout");

        // 下次以同名 AD 帳號驗證時綁定回同一位使用者，不另建。
        using var again = f.CreateClient();
        Assert.Equal(bobId, (await Login(again, "bob")).Id);
        using (var scope = f.Services.CreateScope())
            Assert.Equal(Bob, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == bobId)).Sid);
    }
}
