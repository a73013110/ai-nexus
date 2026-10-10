using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity.Users;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Conversations;

public sealed class ConversationHistoryTests
{
    [Fact]
    public async Task PersonalHistoryMessagesRunsAndEventsAreIsolated()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(alice, "私人對話");
        var run = await CreateRun(alice, conversation.Id, "第一題");
        Assert.Empty((await bob.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
        foreach (var route in new[] { $"conversations/{conversation.Id}", $"runs/{run.Id}", $"runs/{run.Id}/events" })
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/{route}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/runs/{run.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}", new RenameConversationRequest("changed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
    }

    [Fact]
    public async Task HistorySearchRenameDeleteAndPreferencesRoundTrip()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client, "初始標題");
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}", new RenameConversationRequest("會議紀錄"))).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations?search=會議"))!);
        Assert.Empty((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations?search=不存在"))!);
        var preference = new PreferencesDto("dark", true, "test-model");
        (await client.PutAsJsonAsync("/api/v1/preferences", preference)).EnsureSuccessStatusCode();
        Assert.Equal(preference, (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Preferences);
        (await client.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
    }
}
