using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Office.Flow.Approvals;
using static AiNexus.IntegrationTests.Support.FlowApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Flow;

public sealed class ListFlowInstancesTests
{
    private static async Task<FlowInstancePageDto> PageAsync(HttpClient client, string query)
        => (await client.GetFromJsonAsync<FlowInstancePageDto>($"{Base}/instances?{query}"))!;

    [Fact]
    public async Task ViewsShowOwnDecidedOrAllRequestsAndPageByCursor()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave"); using var alice = await session.ClientAsync("alice");
        var first = await SubmitAsync(session, bob);
        (await bob.PostAsync($"{Base}/instances/{first}/cancel", null)).EnsureSuccessStatusCode();
        var second = await SubmitAsync(session, bob);

        Assert.Equal([second, first], (await PageAsync(bob, "view=mine")).Items.Select(x => x.Id));
        Assert.Equal([first], (await PageAsync(bob, $"view=mine&before={second}")).Items.Select(x => x.Id));
        Assert.Equal([second], (await PageAsync(bob, "view=mine&status=pending")).Items.Select(x => x.Id));
        Assert.Empty((await PageAsync(dave, "view=decided")).Items);
        await ApproveAsync(dave, second, 1);
        var decided = Assert.Single((await PageAsync(dave, "view=decided")).Items);
        Assert.Equal(("儲存庫權限", "儲存庫負責人"), (decided.KindName, decided.CurrentStepName));
        Assert.Equal(2, (await PageAsync(alice, "view=all")).Items.Count);

        await AssertCodeAsync(await bob.GetAsync($"{Base}/instances?view=all"), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await bob.GetAsync($"{Base}/instances?view=other"), HttpStatusCode.BadRequest, "flow_view_invalid");
        await AssertCodeAsync(await bob.GetAsync($"{Base}/instances?before={Guid.NewGuid()}"), HttpStatusCode.BadRequest, "flow_cursor_invalid");
    }
}
