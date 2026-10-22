using System.Net;
using AiNexus.Features.Admin.Identity.Provisioning;
using static AiNexus.IntegrationTests.Support.DirectoryApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class SearchDirectoryPeopleTests
{
    [Fact]
    public async Task SearchMatchesDirectoryPeopleAndReportsTheirPlatformState()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var me = await Login(admin, "alice");

        var department = await Search(admin, "研發");
        Assert.False(department.More);
        Assert.Equal(["吳大衛", "林可蘿"], department.People.Select(x => x.DisplayName).Order(StringComparer.Ordinal));
        var dave = Assert.Single(department.People, x => x.Sid == FixtureAdDirectory.Dave);
        Assert.True(dave.Disabled); Assert.Equal(DirectoryPersonStates.New, dave.State); Assert.Null(dave.UserId);
        Assert.Equal("研發處 › 平台部", dave.Path);

        var alice = Assert.Single((await Search(admin, "ali")).People);
        Assert.Equal(DirectoryPersonStates.Linked, alice.State); Assert.Equal(me.Id, alice.UserId);
        Assert.Single((await Search(admin, "可蘿")).People);

        var blank = await Search(admin, "  ");
        Assert.Empty(blank.People); Assert.False(blank.More);
        await AssertCodeAsync(await admin.GetAsync("/api/v1/admin/directory-people?search=" + new string('a', 65)), HttpStatusCode.BadRequest, "invalid_search");
    }

    [Fact]
    public async Task SearchRequiresTheImportGrantAndReportsAnUnavailableDirectory()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var member = f.CreateClient(); await Login(member, "bob");
        await AssertCodeAsync(await member.GetAsync("/api/v1/admin/directory-people?search=carol"), HttpStatusCode.Forbidden, "feature_forbidden");

        using var admin = f.CreateClient(); await Login(admin, "alice");
        f.AdDirectory.Unavailable = true;
        await AssertCodeAsync(await admin.GetAsync("/api/v1/admin/directory-people?search=carol"), HttpStatusCode.ServiceUnavailable, "directory_unavailable");
    }
}
