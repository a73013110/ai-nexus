using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Work.Repositories.Access;
using static AiNexus.IntegrationTests.Support.FlowApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Repositories;

public sealed class RequestRepositoryAccessTests
{
    private static async Task ApproveAllAsync(RepositoryAccessSession session, Guid id)
    {
        using var dave = await session.ClientAsync("dave"); using var carol = await session.ClientAsync("carol");
        await ApproveAsync(dave, id, 1);
        await ApproveAsync(carol, id, 2);
    }

    [Fact]
    public async Task PendingOrCoveredRequestsAreRefusedButUpgradesAreAllowed()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob");
        var first = await SubmitAsync(session, bob, "read");
        var listed = (await bob.GetFromJsonAsync<ManagedRepositoriesDto>(RepositoryAccessSession.Repositories, RepositoryAccessSession.Json))!.Items.Single();
        Assert.Equal((first, RepositoryManager.None), (listed.PendingInstanceId, listed.Manager));
        await AssertCodeAsync(await session.RequestAsync(bob, "write"), HttpStatusCode.Conflict, "repository_access_pending");

        await ApproveAllAsync(session, first);

        await AssertCodeAsync(await session.RequestAsync(bob, "read"), HttpStatusCode.Conflict, "repository_access_held");
        await ApproveAllAsync(session, await SubmitAsync(session, bob, "write"));
        var grants = await session.GrantsAsync();
        Assert.Equal(["read", "write"], grants.Select(x => x.Permission));
        Assert.Equal("replaced", grants[0].EndReason);
        Assert.Null(grants[1].EndedAt);
    }

    [Fact]
    public async Task ClosedRepositoriesAndMissingGiteaAccountsCannotBeRequested()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var alice = await session.ClientAsync("alice"); using var bob = await session.ClientAsync("bob");
        await AssertCodeAsync(await bob.PostAsJsonAsync($"{RepositoryAccessSession.Repositories}/{session.Repository}/requests",
            new RepositoryAccessRequestBody("admin", null, "需要管理")), HttpStatusCode.BadRequest, "validation_failed");
        await AssertCodeAsync(await session.RequestAsync(bob, "read", DateOnly.FromDateTime(DateTime.Today.AddDays(-2))), HttpStatusCode.BadRequest, "repository_access_expiry_invalid");

        session.Gitea.Users.Remove("bob");
        await AssertCodeAsync(await session.RequestAsync(bob, "read"), HttpStatusCode.Conflict, "gitea_account_missing");
        session.Gitea.Users.Add("bob");

        var detail = await session.DetailAsync(alice);
        (await alice.PutAsJsonAsync($"{RepositoryAccessSession.Repositories}/{session.Repository}/settings",
            new ManagedRepositorySettingsRequest(false, [session.Users["carol"]], detail.RowVersion))).EnsureSuccessStatusCode();

        Assert.Empty((await bob.GetFromJsonAsync<ManagedRepositoriesDto>(RepositoryAccessSession.Repositories, RepositoryAccessSession.Json))!.Items);
        await AssertCodeAsync(await session.RequestAsync(bob, "read"), HttpStatusCode.Conflict, "repository_not_requestable");
    }
}
