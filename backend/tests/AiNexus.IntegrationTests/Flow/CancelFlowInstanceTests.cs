using System.Net;
using AiNexus.Data.App.Flow;
using static AiNexus.IntegrationTests.Support.FlowApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Flow;

public sealed class CancelFlowInstanceTests
{
    [Fact]
    public async Task OnlyTheApplicantCancelsAndCanThenRequestAgain()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave");
        var id = await SubmitAsync(session, bob);
        await AssertCodeAsync(await session.RequestAsync(bob, "read"), HttpStatusCode.Conflict, "repository_access_pending");

        await AssertCodeAsync(await dave.PostAsync($"{Base}/instances/{id}/cancel", null), HttpStatusCode.NotFound, "flow_instance_not_found");
        (await bob.PostAsync($"{Base}/instances/{id}/cancel", null)).EnsureSuccessStatusCode();

        Assert.Equal(FlowStatuses.Cancelled, (await InstanceAsync(bob, id)).Status);
        Assert.Empty(await TasksAsync(dave));
        await AssertCodeAsync(await DecideAsync(dave, id, 1, true), HttpStatusCode.Conflict, "flow_instance_closed");
        Assert.NotEqual(id, await SubmitAsync(session, bob));
    }
}
