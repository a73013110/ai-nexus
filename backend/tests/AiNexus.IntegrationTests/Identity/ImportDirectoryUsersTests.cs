using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Provisioning;
using AiNexus.Features.Admin.Identity.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.DirectoryApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class ImportDirectoryUsersTests
{
    private const string Bob = "S-1-5-21-fixture-bob";

    [Fact]
    public async Task ImportCreatesUsersLikeFirstSignInAndRepeatingIsIdempotent()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");

        var created = await Import(admin, FixtureAdDirectory.Carol, Bob);
        Assert.All(created, x => Assert.Equal(DirectoryOutcomes.Create, x.Outcome));
        var carolId = created[0].UserId!.Value; var bobId = created[1].UserId!.Value;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var carol = await db.Users.AsNoTracking().SingleAsync(x => x.Id == carolId);
            Assert.Equal((FixtureAdDirectory.Carol, "carol.lin", "林可蘿"), (carol.Sid, carol.AdAccount, carol.DisplayName));
            Assert.Equal(("carol.lin@example.test", EmailSources.Directory), (carol.Email, carol.EmailSource));
            Assert.NotNull(carol.EmailVerifiedAt);
            Assert.True(carol.Enabled && carol.AdEnabled && !carol.LocalEnabled && !carol.ProfileManaged);
            // 只有本人通過 AD 驗證才算目錄驗證；匯入不能讓本地密碼的新鮮度規則誤以為剛登入過。
            Assert.Null(carol.DirectoryVerifiedAt);
            Assert.Equal(["member"], await db.Set<UserRole>().Where(x => x.UserId == carolId).Select(x => x.RoleId).ToListAsync());
            Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.Action == "admin.user_import" && x.Result == "saved"));
            Assert.False(await db.AuditEvents.AnyAsync(x => x.Action == "identity.first_seen" && x.OwnerId == carolId));
        }

        // 匯入的帳號與首次登入走同一條路：bob 之後登入時以 SID 對到同一位使用者，不會另建一位。
        using var bob = f.CreateClient();
        Assert.Equal(bobId, (await Login(bob, "bob")).Id);

        var again = await Import(admin, FixtureAdDirectory.Carol, Bob);
        Assert.All(again, x => Assert.Equal(DirectoryOutcomes.Unchanged, x.Outcome));
        Assert.Equal([carolId, bobId], again.Select(x => x.UserId!.Value));
        using var check = f.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await database.Users.CountAsync());
        Assert.Equal(2, await database.AuditEvents.CountAsync(x => x.Action == "admin.user_import"));
        Assert.NotNull((await database.Users.SingleAsync(x => x.Id == bobId)).DirectoryVerifiedAt);
    }

    [Fact]
    public async Task PreviewReportsOutcomesWithoutWritingAndDisabledPeopleNeedExplicitConsent()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        f.AdDirectory.Put(f.AdDirectory.Get("S-1-5-21-fixture-alice") with { DisplayName = "陳愛麗" });
        const string missing = "S-1-5-21-1000-2000-3000-9999";

        var preview = await Preview(admin, new([FixtureAdDirectory.Carol, FixtureAdDirectory.Dave, "S-1-5-21-fixture-alice", missing]));
        Assert.Equal([DirectoryOutcomes.Create, DirectoryOutcomes.Disabled, DirectoryOutcomes.Update, DirectoryOutcomes.NotFound], preview.Select(x => x.Outcome));
        Assert.True(preview[1].DirectoryDisabled);
        var rename = Assert.Single(preview[2].Changes);
        Assert.Equal(("displayName", "alice", "陳愛麗"), (rename.Field, rename.Before, rename.After));
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal("alice", (await db.Users.SingleAsync()).DisplayName);
            Assert.False(await db.AuditEvents.AnyAsync(x => x.Action == "admin.user_import"));
        }

        Assert.Equal(DirectoryOutcomes.Create, Assert.Single(await Preview(admin, new([FixtureAdDirectory.Dave], IncludeDisabled: true))).Outcome);
        Assert.Equal(DirectoryOutcomes.Disabled, Assert.Single(await Import(admin, FixtureAdDirectory.Dave)).Outcome);
        var dave = Assert.Single(await Import(admin, new DirectoryImportRequest([FixtureAdDirectory.Dave], IncludeDisabled: true)));
        Assert.Equal(DirectoryOutcomes.Create, dave.Outcome);
        Assert.True(dave.DirectoryDisabled);
    }

    [Fact]
    public async Task ImportFollowsTheSidWhenTheAccountIsRenamedAndBindsPrebuiltAccounts()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var carolId = Assert.Single(await Import(admin, FixtureAdDirectory.Carol)).UserId!.Value;

        f.AdDirectory.Put(f.AdDirectory.Get(FixtureAdDirectory.Carol) with { AccountName = "carol.wang", DisplayName = "王可蘿" });
        var renamed = Assert.Single(await Import(admin, FixtureAdDirectory.Carol));
        Assert.Equal((DirectoryOutcomes.Update, (Guid?)carolId), (renamed.Outcome, renamed.UserId));
        Assert.Equal(["adAccount", "displayName"], renamed.Changes.Select(x => x.Field));

        // 管理員預建、尚未登入的同名帳號：匯入綁定 SID，保留管理員填的姓名。
        var prebuiltId = await CreateUser(admin, new UserAccountRequest("陳艾琳（預建）", true, true, false, "erin.chen", null, ["member"]));
        var bound = Assert.Single(await Import(admin, FixtureAdDirectory.Erin));
        Assert.Equal((DirectoryOutcomes.Bind, (Guid?)prebuiltId), (bound.Outcome, bound.UserId));

        // 同名但不同 SID 的人不能接管已連結的帳號；改名撞到別人已用的 AD 帳號時，AdAccount 保留原值。
        const string impostor = "S-1-5-21-1000-2000-3000-1104";
        f.AdDirectory.Put(new(impostor, "CN=王可蘿二號,OU=業務處," + FixtureAdDirectory.Root, "carol.wang", "王可蘿二號", null, null, null, false));
        Assert.Equal(DirectoryOutcomes.Conflict, Assert.Single(await Import(admin, impostor)).Outcome);
        f.AdDirectory.Put(f.AdDirectory.Get(FixtureAdDirectory.Carol) with { AccountName = "erin.chen" });
        Assert.Equal(DirectoryOutcomes.Unchanged, Assert.Single(await Import(admin, FixtureAdDirectory.Carol)).Outcome);

        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var carol = await db.Users.AsNoTracking().SingleAsync(x => x.Id == carolId);
        Assert.Equal(("carol.wang", "王可蘿"), (carol.AdAccount, carol.DisplayName));
        var erin = await db.Users.AsNoTracking().SingleAsync(x => x.Id == prebuiltId);
        Assert.Equal((FixtureAdDirectory.Erin, "陳艾琳（預建）", true), (erin.Sid, erin.DisplayName, erin.ProfileManaged));
        Assert.False(await db.Users.AnyAsync(x => x.Sid == impostor));
    }

    [Fact]
    public async Task ImportIsLimitedToGrantedAdministratorsOutsideTestIdentityAndWritesNothingWithoutTheDirectory()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var member = f.CreateClient(); await Login(member, "bob");
        await AssertCodeAsync(await member.PostAsJsonAsync(Imports, new DirectoryImportRequest([FixtureAdDirectory.Carol])), HttpStatusCode.Forbidden, "feature_forbidden");

        using var admin = f.CreateClient(); await Login(admin, "alice");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Imports, new DirectoryImportRequest([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Imports, new DirectoryImportRequest([FixtureAdDirectory.Carol, FixtureAdDirectory.Carol]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Imports, new DirectoryImportRequest(["carol.lin"]))).StatusCode);
        var tooMany = Enumerable.Range(0, 101).Select(i => $"S-1-5-21-1000-2000-3000-{2000 + i}").ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Imports, new DirectoryImportRequest(tooMany))).StatusCode);

        f.AdDirectory.Unavailable = true;
        await AssertCodeAsync(await admin.PostAsJsonAsync(Imports, new DirectoryImportRequest([FixtureAdDirectory.Carol])), HttpStatusCode.ServiceUnavailable, "directory_unavailable");
        f.AdDirectory.Unavailable = false;

        // 已刪除的帳號保留 SID 防止接管，匯入不會悄悄復原。
        var carolId = Assert.Single(await Import(admin, FixtureAdDirectory.Carol)).UserId!.Value;
        (await admin.DeleteAsync($"/api/v1/admin/users/{carolId}")).EnsureSuccessStatusCode();
        Assert.Equal(DirectoryOutcomes.Deleted, Assert.Single(await Import(admin, FixtureAdDirectory.Carol)).Outcome);

        var target = await CreateUser(admin, LocalAccount(roles: ["member", "administrator"]));
        (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(target, "檢查匯入權限"))).EnsureSuccessStatusCode();
        await Csrf(admin);
        (await admin.GetAsync(People + "?node=" + Uri.EscapeDataString(FixtureAdDirectory.Root) + "&subtree=true")).EnsureSuccessStatusCode();
        await AssertCodeAsync(await admin.PostAsJsonAsync(Imports, new DirectoryImportRequest([FixtureAdDirectory.Erin])), HttpStatusCode.Forbidden, "test_admin_read_only");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Syncs, new DirectorySyncRequest([target])), HttpStatusCode.Forbidden, "test_admin_read_only");

        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Sid == FixtureAdDirectory.Erin));
    }
}
