using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Conversations;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Conversations;

public sealed class ConversationSettingsTests
{
    [Fact]
    public async Task FavoriteArchiveLabelsAndMessageSearchRoundTrip()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client, "不同的標題");
        await WaitForTerminal(client, (await CreateRun(client, conversation.Id, "內容關鍵字 ROADMAP")).Id);
        var settings = new ConversationSettingsRequest(true, false, "回答要附上待辦事項", ["工作", "工作", "規劃"]);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", settings)).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations?view=favorites&search=ROADMAP&label=工作"))!);
        Assert.Equal(2, (await client.GetFromJsonAsync<List<string>>("/api/v1/conversations/labels"))!.Count);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", new ConversationSettingsRequest(IsArchived: true, Labels: ["工作"])) ).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations?view=archived"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await PostRun(client, new(conversation.Id, "test-model", "new", null, null))).StatusCode);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", new ConversationSettingsRequest(IsArchived: false))).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
    }

    [Fact]
    public async Task ConversationInstructionIsSnapshottedIntoGeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", new ConversationSettingsRequest(SystemInstruction: "先摘要，再列待辦"))).EnsureSuccessStatusCode();
        await WaitForTerminal(client, (await CreateRun(client, conversation.Id, "hello")).Id);
        Assert.Contains("先摘要，再列待辦", factory.Provider.LastParameters!.SystemPrompt);
    }
}
