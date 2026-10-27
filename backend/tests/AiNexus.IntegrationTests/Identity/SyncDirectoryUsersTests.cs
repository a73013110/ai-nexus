using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.DirectoryApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class SyncDirectoryUsersTests
{
    private const string Bob = "S-1-5-21-fixture-bob";

    [Fact]
    public async Task SyncAppliesTheDirectoryButKeepsAdministratorEmailAndOnlyReportsMissingOrDisabledPeople()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var imported = await Import(admin, new DirectoryImportRequest([FixtureAdDirectory.Carol, Bob, FixtureAdDirectory.Erin, FixtureAdDirectory.Dave], IncludeDisabled: true));
        var ids = imported.Select(x => x.UserId!.Value).ToArray();
        var (carolId, bobId, erinId, daveId) = (ids[0], ids[1], ids[2], ids[3]);
        var prebuiltId = await CreateUser(admin, LocalAccount("prebuilt"));
        // bob 在 AD 沒有信箱，管理員替他設定；之後 AD 補上 mail 也不覆寫管理員的值。
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{bobId}/email", new UserEmailRequest("bob.personal@example.test"))).EnsureSuccessStatusCode();

        f.AdDirectory.Put(f.AdDirectory.Get(FixtureAdDirectory.Carol) with { DisplayName = "林可蘿（平台部）", Email = "carol.new@example.test" });
        f.AdDirectory.Put(f.AdDirectory.Get(Bob) with { DisplayName = "鮑伯", Email = "bob@example.test" });
        f.AdDirectory.Remove(FixtureAdDirectory.Erin);

        var synced = await Sync(admin, carolId, bobId, erinId, daveId, prebuiltId);
        Assert.Equal([DirectoryOutcomes.Update, DirectoryOutcomes.Update, DirectoryOutcomes.NotFound, DirectoryOutcomes.Unchanged, DirectoryOutcomes.NotLinked], synced.Select(x => x.Outcome));
        Assert.Equal(["displayName", "email"], synced[0].Changes.Select(x => x.Field));
        Assert.Equal(["displayName"], synced[1].Changes.Select(x => x.Field));
        Assert.True(synced[3].DirectoryDisabled);
        Assert.Equal([true, true], new[] { synced[2].Enabled, synced[3].Enabled });

        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var carol = await db.Users.AsNoTracking().SingleAsync(x => x.Id == carolId);
        Assert.Equal(("林可蘿（平台部）", "carol.new@example.test", EmailSources.Directory), (carol.DisplayName, carol.Email, carol.EmailSource));
        var bob = await db.Users.AsNoTracking().SingleAsync(x => x.Id == bobId);
        Assert.Equal(("鮑伯", "bob.personal@example.test", EmailSources.Admin), (bob.DisplayName, bob.Email, bob.EmailSource));
        // AD 找不到或已停用只回報，不自動停用平台帳號。
        Assert.True(await db.Users.Where(x => x.Id == erinId || x.Id == daveId).AllAsync(x => x.Enabled));
        Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.Action == "identity.user.synced"));
    }

    [Fact]
    public async Task SyncRequiresTheSyncGrantAndValidatesTheSelection()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var member = f.CreateClient(); var bob = await Login(member, "bob");
        await AssertCodeAsync(await member.PostAsJsonAsync(Syncs, new DirectorySyncRequest([bob.Id])), HttpStatusCode.Forbidden, "feature_forbidden");

        using var admin = f.CreateClient(); await Login(admin, "alice");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Syncs, new DirectorySyncRequest([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Syncs, new DirectorySyncRequest([bob.Id, bob.Id]))).StatusCode);
        f.AdDirectory.Unavailable = true;
        await AssertCodeAsync(await admin.PostAsJsonAsync(Syncs, new DirectorySyncRequest([bob.Id])), HttpStatusCode.ServiceUnavailable, "directory_unavailable");
    }
}
