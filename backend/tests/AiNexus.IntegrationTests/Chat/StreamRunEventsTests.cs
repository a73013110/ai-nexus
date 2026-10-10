using System.Text.Json;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class StreamRunEventsTests
{
    [Fact]
    public async Task ExpiredReplayEventsRecoverWithAnAuthoritativeSnapshot()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var run = await CreateRun(client, conversation.Id, "快照");
        var completed = await WaitForTerminal(client, run.Id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        await db.RunEvents.Where(x => x.RunId == run.Id).ExecuteDeleteAsync();
        var events = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after=0");
        Assert.Contains("snapshot", events);
        var data = events.Split('\n').Single(x => x.StartsWith("data: ", StringComparison.Ordinal))[6..];
        Assert.Equal(completed.Content, JsonSerializer.Deserialize<RunEventDto>(data, JsonSerializerOptions.Web)!.Delta);
    }

    [Fact]
    public async Task APartiallyExpiredReplayIsRecoveredAsAFullSnapshot()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var conversation = await ChatApi.CreateConversation(client);
        var run = await ChatApi.CreateRun(client, conversation.Id, "保留缺口"); var complete = await ChatApi.WaitForTerminal(client, run.Id);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        await db.RunEvents.Where(x => x.RunId == run.Id && x.Sequence <= 2).ExecuteDeleteAsync();
        var events = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after=0");
        Assert.Contains("snapshot", events);
        var data = events.Split('\n').Single(x => x.StartsWith("data: ", StringComparison.Ordinal))[6..];
        Assert.Equal(complete.Content, JsonSerializer.Deserialize<RunEventDto>(data, JsonSerializerOptions.Web)!.Delta);
    }
}
