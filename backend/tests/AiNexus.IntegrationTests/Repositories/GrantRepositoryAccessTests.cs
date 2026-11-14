using System.Net;
using AiNexus.Data.App.Repositories;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class GrantRepositoryAccessTests
{
    [Fact]
    public async Task StewardsGrantPeopleWhileAdminPermissionAndServiceAccountsNeedAnAdministrator()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var carol = await session.ClientAsync("carol"); using var alice = await session.ClientAsync("alice"); using var dave = await session.ClientAsync("dave");
        var bob = session.Users["bob"];

        await AssertCodeAsync(await session.GrantAsync(dave, bob, "read"), HttpStatusCode.Forbidden, "repository_access_forbidden");
        await AssertCodeAsync(await session.GrantAsync(carol, bob, "admin"), HttpStatusCode.Forbidden, "repository_access_forbidden");
        await AssertCodeAsync(await session.GrantAsync(carol, null, "read", "deploy-bot", "部署"), HttpStatusCode.Forbidden, "repository_access_forbidden");
        await AssertCodeAsync(await session.GrantAsync(alice, null, "read", "deploy-bot"), HttpStatusCode.BadRequest, "repository_grantee_invalid");
        await AssertCodeAsync(await session.GrantAsync(alice, null, "read", "bob", "人員要授權給本人"), HttpStatusCode.BadRequest, "repository_grantee_invalid");
        (await session.GrantAsync(carol, bob, "read")).EnsureSuccessStatusCode();
        (await session.GrantAsync(carol, bob, "write")).EnsureSuccessStatusCode();
        (await session.GrantAsync(alice, null, "read", "deploy-bot", "CI 部署用")).EnsureSuccessStatusCode();

        var grants = await session.GrantsAsync();
        Assert.Equal([("read", "replaced"), ("write", null), ("read", null)], grants.Select(x => (x.Permission, x.EndReason)));
        Assert.Equal([RepositoryGrantSources.Steward, RepositoryGrantSources.Steward, RepositoryGrantSources.Admin], grants.Select(x => x.Source));
        await session.SyncAsync();
        Assert.Equal(new Dictionary<string, string> { ["bob"] = "write", ["deploy-bot"] = "read" }, session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus));
        var detail = await session.DetailAsync(carol);
        Assert.Equal(["bob", "deploy-bot"], detail.Grants.Select(x => x.Login).Order());
        Assert.Empty((await session.DetailAsync(dave)).Grants);
    }

    [Fact]
    public async Task UnknownGiteaAccountsAndInactivePeopleAreRefused()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var carol = await session.ClientAsync("carol");

        session.Gitea.Users.Remove("bob");
        await AssertCodeAsync(await session.GrantAsync(carol, session.Users["bob"], "read"), HttpStatusCode.Conflict, "gitea_account_missing");
        await IdentityApi.DisableAsync(session.Factory, session.Users["dave"]);
        await AssertCodeAsync(await session.GrantAsync(carol, session.Users["dave"], "read"), HttpStatusCode.BadRequest, "repository_grantee_invalid");
        Assert.Empty(await session.GrantsAsync());
    }
}
