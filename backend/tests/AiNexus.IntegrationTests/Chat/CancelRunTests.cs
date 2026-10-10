using System.Globalization;
using System.Net.Http.Json;
using AiNexus.Features.Inference;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class CancelRunTests
{
    [Fact]
    public async Task CancelPreservesPartialOutputAndReplayHasIncreasingSequences()
    {
        await using var factory = new NexusFactory();
        factory.Provider.DelayMs = 150;
        factory.Provider.NeverFinish = true;
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var run = await CreateRun(client, conversation.Id, "取消測試");
        await WaitForFirstDelta(client, run.Id);
        Assert.NotEmpty((await client.GetFromJsonAsync<RunDto>($"/api/v1/runs/{run.Id}"))!.Content);
        var response = await client.PostAsync($"/api/v1/runs/{run.Id}/cancel", null);
        response.EnsureSuccessStatusCode();
        var cancelled = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        Assert.Equal(RunStates.Cancelled, cancelled.Status);
        Assert.NotEmpty(cancelled.Content);
        var events = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after=0");
        var ids = events.Split('\n').Where(x => x.StartsWith("id: ", StringComparison.Ordinal)).Select(x => long.Parse(x[4..], CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(ids.Distinct().Order(), ids);
        Assert.Contains("cancelled", events);
        var resumed = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after={ids[0]}");
        Assert.DoesNotContain($"id: {ids[0]}\n", resumed);
    }

    [Fact]
    public async Task CancellingQueuedWorkPreventsItsProviderRequest()
    {
        await using var factory = new NexusFactory();
        factory.Provider.DelayMs = 200; factory.Provider.NeverFinish = true;
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var a = await ChatApi.CreateConversation(alice); var b = await ChatApi.CreateConversation(bob);
        var first = await ChatApi.CreateRun(alice, a.Id, "A"); var second = await ChatApi.CreateRun(bob, b.Id, "B");
        (await bob.PostAsync($"/api/v1/runs/{second.Id}/cancel", null)).EnsureSuccessStatusCode();
        (await alice.PostAsync($"/api/v1/runs/{first.Id}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Equal(RunStates.Cancelled, (await ChatApi.WaitForTerminal(bob, second.Id)).Status);
        var budget = Assert.Single((await bob.GetFromJsonAsync<AiNexus.Features.Inference.EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(0, budget.ReservedTokens);
        await Task.Delay(100); Assert.True(factory.Provider.Calls <= 1);
    }
}
