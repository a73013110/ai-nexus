using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Identity.Provisioning;
using static AiNexus.IntegrationTests.Support.DirectoryApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class ListAccountReviewsTests
{
    [Fact]
    public async Task EnabledAccountsWhoseAdAccountIsDisabledOrMissingAreListedUntilAnAdministratorDisablesThem()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        var imported = await Import(admin, new DirectoryImportRequest([FixtureAdDirectory.Carol, FixtureAdDirectory.Dave, FixtureAdDirectory.Erin], IncludeDisabled: true));
        var (carol, dave, erin) = (imported[0].UserId!.Value, imported[1].UserId!.Value, imported[2].UserId!.Value);
        f.AdDirectory.Remove(FixtureAdDirectory.Erin);

        var reviews = await ReviewsAsync(admin);
        Assert.Empty(reviews.Notices);
        Assert.Equal(new[] { dave, erin }.Order(), reviews.Users.Select(x => x.Id).Order());
        Assert.Equal(["identity.directory_disabled"], await ReviewCodesAsync(admin, dave));
        var missing = reviews.Users.Single(x => x.Id == erin);
        Assert.Equal((true, false), (missing.Enabled, missing.Deleted));
        Assert.Equal("identity.directory_missing", missing.Items.Single().Code);
        Assert.Null(missing.Items.Single().Route);
        Assert.Empty(await ReviewCodesAsync(admin, carol));

        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{dave}", new UserAccountRequest("吳大衛", false, true, false, "dave.wu", null, ["member"]))).EnsureSuccessStatusCode();
        Assert.Empty(await ReviewCodesAsync(admin, dave));

        using var member = f.CreateClient(); await Login(member, "bob");
        await AssertCodeAsync(await member.GetAsync("/api/v1/admin/account-reviews"), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    [Fact]
    public async Task TooManyMissingPeopleOrAnUnavailableDirectoryAreReportedInsteadOfListed()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");
        string[] extras = [.. Enumerable.Range(1, 5).Select(i => $"S-1-5-21-1000-2000-3000-12{i:00}")];
        foreach (var (sid, i) in extras.Select((x, i) => (x, i)))
            f.AdDirectory.Put(new DirectoryPerson(sid, $"CN=fixture{i},{FixtureAdDirectory.Users}", $"fixture.{i}", $"虛構人員{i}", null, null, null, false));
        await Import(admin, [FixtureAdDirectory.Erin, .. extras]);
        foreach (var sid in extras.Append(FixtureAdDirectory.Erin)) f.AdDirectory.Remove(sid);

        // 6 位查不到、超過下限 5 位與 10%：視為目錄查詢不完整，只說明、不列出。
        var reviews = await ReviewsAsync(admin);
        Assert.Empty(reviews.Users);
        Assert.Contains("6 位", Assert.Single(reviews.Notices));

        f.AdDirectory.Unavailable = true;
        reviews = await ReviewsAsync(admin);
        Assert.Empty(reviews.Users);
        Assert.Contains("暫時無法讀取", Assert.Single(reviews.Notices));
    }
}
