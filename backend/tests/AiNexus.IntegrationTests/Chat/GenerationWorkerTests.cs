using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class GenerationWorkerTests
{
    [Fact]
    public async Task ProviderFailureAndTimeoutBecomeTerminalAndAllowRetry()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new NexusFactory(clock: clock);
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        factory.Provider.Fail = true;
        var first = await CreateRun(client, conversation.Id, "失敗");
        var failure = await WaitForTerminal(client, first.Id);
        Assert.Equal("provider_connection_lost", failure.ErrorCode);
        factory.Provider.Fail = false;
        factory.Provider.NeverFinish = true;
        var second = await CreateRun(client, conversation.Id, "逾時");
        await WaitForFirstDelta(client, second.Id);
        clock.Advance(TimeSpan.FromSeconds(5));
        var timedOut = await WaitForTerminal(client, second.Id);
        Assert.Equal("generation_timeout", timedOut.ErrorCode);
        Assert.NotEmpty(timedOut.Content);
    }
}
