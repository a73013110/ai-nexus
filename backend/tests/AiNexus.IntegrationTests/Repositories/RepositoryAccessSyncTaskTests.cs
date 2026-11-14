using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Work.Repositories.Access;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class RepositoryAccessSyncTaskTests
{
    [Fact]
    public async Task CatalogFollowsGiteaAndUnregisteredCollaboratorsAreListedWithoutBeingRemoved()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var alice = await session.ClientAsync("alice"); using var bob = await session.ClientAsync("bob");
        session.Gitea.AddRepository(2, "tools", "hanglong", "developers");
        session.Gitea.AddCollaboratorDirectly(RepositoryAccessSession.Nexus, "eve", "write");

        await session.SyncAsync();

        var all = (await alice.GetFromJsonAsync<ManagedRepositoriesDto>(RepositoryAccessSession.Repositories, RepositoryAccessSession.Json))!;
        Assert.True(all.Enabled && all.Administrator);
        var tools = all.Items.Single(x => x.Name == "tools");
        Assert.Equal((true, "developers", false), (tools.Requestable, tools.GrantingTeams, tools.Missing));
        var eve = Assert.Single((await session.DetailAsync(alice)).Unregistered);
        Assert.Equal(("eve", "write", null), (eve.Login, eve.Permission, eve.User));
        Assert.Equal("write", session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus)["eve"]);

        session.Gitea.RemoveRepository(2);
        await session.SyncAsync();

        Assert.True((await alice.GetFromJsonAsync<ManagedRepositoriesDto>(RepositoryAccessSession.Repositories, RepositoryAccessSession.Json))!.Items.Single(x => x.Name == "tools").Missing);
        Assert.Equal(["nexus"], (await bob.GetFromJsonAsync<ManagedRepositoriesDto>(RepositoryAccessSession.Repositories, RepositoryAccessSession.Json))!.Items.Select(x => x.Name));
    }

    [Fact]
    public async Task ExpiredGrantsAreEndedAndRemovedAfterAnExpiryNotice()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var session = await RepositoryAccessSession.StartAsync(clock);
        using var carol = await session.ClientAsync("carol");
        var today = RepositoryAccess.ExpiryDate(clock.GetUtcNow());
        (await carol.PostAsJsonAsync($"{RepositoryAccessSession.Repositories}/{session.Repository}/grants",
            new RepositoryGrantRequest(session.Users["bob"], null, "read", today, null))).EnsureSuccessStatusCode();

        await session.SyncAsync();
        Assert.Equal("read", session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus)["bob"]);
        using (var scope = session.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Notifications.AnyAsync(x => x.OwnerId == session.Users["bob"] && x.EventKey.StartsWith("repository-access:expiring:")));
        }

        clock.Advance(TimeSpan.FromDays(2));
        await session.SyncAsync();

        var grant = Assert.Single(await session.GrantsAsync());
        Assert.Equal((RepositoryGrantEndReasons.Expired, RepositorySyncStates.Applied), (grant.EndReason, grant.SyncState));
        Assert.Empty(session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus));
    }

    [Fact]
    public async Task GiteaOutageLeavesGrantsPendingForTheNextRun()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var carol = await session.ClientAsync("carol");
        (await session.GrantAsync(carol, session.Users["bob"], "write")).EnsureSuccessStatusCode();
        session.Gitea.Failure = Error.Unavailable("gitea_unavailable");

        await Assert.ThrowsAsync<ExternalServiceException>(session.SyncAsync);
        Assert.Equal(RepositorySyncStates.Pending, Assert.Single(await session.GrantsAsync()).SyncState);

        session.Gitea.Failure = null;
        await session.SyncAsync();
        Assert.Equal(RepositorySyncStates.Applied, Assert.Single(await session.GrantsAsync()).SyncState);
        Assert.Equal("write", session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus)["bob"]);
    }

    [Fact]
    public async Task GrantsForAccountsGiteaNoLongerHasAreMarkedFailed()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var carol = await session.ClientAsync("carol"); using var alice = await session.ClientAsync("alice");
        (await session.GrantAsync(carol, session.Users["bob"], "read")).EnsureSuccessStatusCode();
        session.Gitea.Users.Remove("bob");

        await session.SyncAsync();

        var grant = Assert.Single((await session.DetailAsync(alice)).Grants);
        Assert.Equal((RepositorySyncStates.Failed, "gitea_account_missing"), (grant.SyncState, grant.SyncError));
    }
}
