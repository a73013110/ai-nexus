using System.Net;
using AiNexus.Features.Admin.Identity.Provisioning;
using static AiNexus.IntegrationTests.Support.DirectoryApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class ListDirectoryTests
{
    [Fact]
    public async Task TreeListsOneLevelWithPeopleCountedThroughTheSubtree()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");

        var root = await Children(admin);
        Assert.Equal(FixtureAdDirectory.Root, root.Node.Dn);
        Assert.Equal("fixture.internal", root.Node.Name);
        Assert.Equal(DirectoryNodeKinds.Root, root.Node.Kind);
        Assert.Equal(5, root.Node.People);
        Assert.False(root.Writable);
        Assert.Equal(["Computers", "Users", "業務處", "研發處", "資訊處"], root.Children.Select(x => x.Name));
        var research = Assert.Single(root.Children, x => x.Dn == FixtureAdDirectory.Research);
        Assert.Equal(2, research.People); Assert.True(research.HasChildren); Assert.Equal(DirectoryNodeKinds.OrganizationalUnit, research.Kind);
        var computers = Assert.Single(root.Children, x => x.Name == "Computers");
        Assert.Equal(0, computers.People); Assert.False(computers.HasChildren); Assert.Equal(DirectoryNodeKinds.Container, computers.Kind);

        var branch = await Children(admin, FixtureAdDirectory.Research);
        Assert.Equal("研發處", branch.Node.Name);
        Assert.Equal(FixtureAdDirectory.Platform, Assert.Single(branch.Children).Dn);
    }

    [Fact]
    public async Task PeopleListDirectOrSubtreeMembersWithTheirPlatformStateAndPendingChanges()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); var me = await Login(admin, "alice");

        Assert.Empty((await List(admin, FixtureAdDirectory.Research)).People);
        var team = await List(admin, FixtureAdDirectory.Research, subtree: true);
        Assert.False(team.More);
        Assert.Equal(["吳大衛", "林可蘿"], team.People.Select(x => x.DisplayName));
        var dave = team.People[0];
        Assert.True(dave.Disabled); Assert.Equal(DirectoryPersonStates.New, dave.State); Assert.Null(dave.UserId); Assert.Empty(dave.Changes);
        Assert.Equal("研發處 › 平台部", dave.Path);
        Assert.Equal("carol.lin@fixture.internal", team.People[1].Upn);

        var it = "OU=資訊處," + FixtureAdDirectory.Root;
        var alice = Assert.Single((await List(admin, it)).People, x => x.Account == "alice");
        Assert.Equal(DirectoryPersonStates.Synced, alice.State); Assert.Equal(me.Id, alice.UserId); Assert.Empty(alice.Changes);

        f.AdDirectory.Put(f.AdDirectory.Get("S-1-5-21-fixture-alice") with { DisplayName = "陳愛麗" });
        alice = Assert.Single((await List(admin, it)).People, x => x.Account == "alice");
        Assert.Equal(DirectoryPersonStates.Changed, alice.State);
        var change = Assert.Single(alice.Changes);
        Assert.Equal(("displayName", "alice", "陳愛麗"), (change.Field, change.Before, change.After));
    }

    [Fact]
    public async Task NodesOutsideTheSearchBaseOrGoneFromTheDirectoryAreRejected()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = f.CreateClient(); await Login(admin, "alice");

        await AssertCodeAsync(await admin.GetAsync($"{Nodes}?parent={Uri.EscapeDataString("DC=other,DC=test")}"), HttpStatusCode.BadRequest, "directory_node_invalid");
        await AssertCodeAsync(await admin.GetAsync($"{People}?node={Uri.EscapeDataString("OU=x,DC=notfixture,DC=internal")}"), HttpStatusCode.BadRequest, "directory_node_invalid");
        var gone = Uri.EscapeDataString("OU=已裁撤," + FixtureAdDirectory.Root);
        await AssertCodeAsync(await admin.GetAsync($"{Nodes}?parent={gone}"), HttpStatusCode.NotFound, "directory_node_not_found");
        await AssertCodeAsync(await admin.GetAsync($"{People}?node={gone}"), HttpStatusCode.NotFound, "directory_node_not_found");
    }

    [Fact]
    public async Task BrowsingRequiresTheImportGrantAndReportsAnUnavailableDirectory()
    {
        await using var f = new AppFactory(ldap: true, administrators: ["alice"]);
        using var member = f.CreateClient(); await Login(member, "bob");
        await AssertCodeAsync(await member.GetAsync(Nodes), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.GetAsync($"{People}?node={Uri.EscapeDataString(FixtureAdDirectory.Users)}"), HttpStatusCode.Forbidden, "feature_forbidden");

        using var admin = f.CreateClient(); await Login(admin, "alice");
        f.AdDirectory.Unavailable = true;
        await AssertCodeAsync(await admin.GetAsync(Nodes), HttpStatusCode.ServiceUnavailable, "directory_unavailable");
    }
}
