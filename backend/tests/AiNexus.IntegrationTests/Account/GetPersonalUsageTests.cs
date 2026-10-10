using System.Net.Http.Json;
using AiNexus.Features.Account;

namespace AiNexus.IntegrationTests.Account;

public sealed class GetPersonalUsageTests
{
    [Fact]
    public async Task UsageCountsOnlyOwnProviderReportedTokens()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var convo = await ChatApi.CreateConversation(alice);
        var run = await ChatApi.CreateRun(alice, convo.Id, "統計");
        await ChatApi.WaitForTerminal(alice, run.Id);
        var own = (await alice.GetFromJsonAsync<PersonalUsageDto>("/api/v1/settings/usage"))!;
        Assert.Equal(1, own.Requests); Assert.Equal(123, own.InputTokens); Assert.Equal(6, own.OutputTokens); Assert.Equal(1, own.RequestsWithUsage);
        var empty = (await bob.GetFromJsonAsync<PersonalUsageDto>("/api/v1/settings/usage"))!;
        Assert.Equal(0, empty.Requests); Assert.Equal(0, empty.InputTokens); Assert.Equal(0, empty.RequestsWithUsage);
    }
}
