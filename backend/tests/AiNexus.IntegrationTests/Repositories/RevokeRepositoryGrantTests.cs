using System.Net;
using AiNexus.Data.App.Repositories;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class RevokeRepositoryGrantTests
{
    [Fact]
    public async Task RevokedAndReleasedGrantsAreRemovedFromGiteaByTheNextSync()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var carol = await session.ClientAsync("carol"); using var dave = await session.ClientAsync("dave"); using var bob = await session.ClientAsync("bob");
        (await session.GrantAsync(carol, session.Users["bob"], "write")).EnsureSuccessStatusCode();
        (await session.GrantAsync(carol, session.Users["dave"], "read")).EnsureSuccessStatusCode();
        await session.SyncAsync();
        var grants = await session.GrantsAsync();
        string Path(RepositoryGrant grant) => $"{RepositoryAccessSession.Repositories}/{session.Repository}/grants/{grant.Id}";

        await AssertCodeAsync(await bob.DeleteAsync(Path(grants[1])), HttpStatusCode.NotFound, "repository_grant_not_found");
        (await carol.DeleteAsync(Path(grants[0]))).EnsureSuccessStatusCode();
        (await dave.DeleteAsync(Path(grants[1]))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await carol.DeleteAsync(Path(grants[0])), HttpStatusCode.NotFound, "repository_grant_not_found");

        Assert.Equal([RepositoryGrantEndReasons.Revoked, RepositoryGrantEndReasons.Released], (await session.GrantsAsync()).Select(x => x.EndReason));
        Assert.Equal(2, (await session.DetailAsync(carol)).Grants.Count(x => x.EndedAt is not null));
        await session.SyncAsync();
        Assert.Empty(session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus));
        Assert.All(await session.GrantsAsync(), x => Assert.Equal(RepositorySyncStates.Applied, x.SyncState));
        Assert.Empty((await session.DetailAsync(carol)).Grants);
    }
}
