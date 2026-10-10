using System.Net;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Inference;

public sealed class GenerationSchedulerTests
{
    [Fact]
    public async Task QueueIsBoundedAndOnlyOneProviderCallRunsAtATime()
    {
        await using var factory = new NexusFactory();
        factory.Provider.Hold();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        using var charlie = await factory.SignedInAsync("charlie");
        var a = await CreateConversation(alice);
        var b = await CreateConversation(bob);
        var c = await CreateConversation(charlie);
        var first = await CreateRun(alice, a.Id, "A");
        Assert.Equal(HttpStatusCode.Conflict, (await PostRun(alice, new(a.Id, "test-model", "second", null, null))).StatusCode);
        var second = await CreateRun(bob, b.Id, "B");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostRun(charlie, new(c.Id, "test-model", "C", null, null))).StatusCode);
        factory.Provider.Release();
        await WaitForTerminal(alice, first.Id);
        await WaitForTerminal(bob, second.Id);
        Assert.Equal(1, factory.Provider.MaxConcurrent);
    }
}
