using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Inference;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class PreviewContextTests
{
    [Fact]
    public async Task ContextPreviewEnforcesOwnershipAndPreservesHistoryWhileTrimming()
    {
        await using var factory = new NexusFactory(inference: options =>
        {
            options.Models[0].ContextTokens = 1024;
            options.Models[0].MaxOutputTokens = 256;
        });
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(alice);
        var first = await CreateRun(alice, conversation.Id, new string('中', 140));
        await WaitForTerminal(alice, first.Id);
        var denied = await bob.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(conversation.Id, first.AssistantMessageId, "追問", "test-model"));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var preview = await alice.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(conversation.Id, first.AssistantMessageId, new string('中', 140), "test-model"));
        preview.EnsureSuccessStatusCode();
        var usage = (await preview.Content.ReadFromJsonAsync<ContextUsageDto>())!;
        Assert.Equal(2, usage.DroppedMessages);
        Assert.True(usage.IsEstimate);
        Assert.False(usage.BudgetExceeded);
        Assert.True(usage.EstimatedInputTokens + usage.ReservedOutputTokens <= usage.ContextTokens);
        var root = await alice.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(conversation.Id, null, "新根提問", "test-model"));
        Assert.Equal(0, (await root.Content.ReadFromJsonAsync<ContextUsageDto>())!.DroppedMessages);
        var huge = await alice.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(conversation.Id, null, new string('中', 400), "test-model"));
        Assert.True((await huge.Content.ReadFromJsonAsync<ContextUsageDto>())!.BudgetExceeded);
        Assert.Equal(2, (await alice.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages.Count);
        Assert.Equal(1, factory.Provider.Calls);
    }

    [Fact]
    public async Task LongHistorySendsOnlyTheNewestCompleteRoundsThatFit()
    {
        await using var factory = new NexusFactory(inference: options => { options.Models[0].ContextTokens = 1024; options.Models[0].MaxOutputTokens = 256; });
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        Guid? parent = null;
        for (var round = 1; round <= 5; round++)
        {
            // About 270 budget units per round: with the system prompt and the new prompt, only one earlier round fits.
            var run = await WaitForTerminal(client, (await CreateRun(client, conversation.Id, new string('中', 60) + round, parent)).Id);
            parent = run.AssistantMessageId;
        }
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, factory.Provider.LastMessages.Select(x => x.Role));
        Assert.Equal(new[] { new string('中', 60) + 4, new string('中', 60) + 5 }, factory.Provider.LastMessages.Where(x => x.Role == "user").Select(x => x.Content));
        var preview = await client.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(conversation.Id, parent, new string('中', 60) + 6, "test-model"));
        Assert.Equal(8, (await preview.Content.ReadFromJsonAsync<ContextUsageDto>())!.DroppedMessages);
    }

    [Fact]
    public async Task BrowserReceivesTheConfiguredPromptCharacterLimit()
    {
        await using var factory = new NexusFactory(inference: x => x.MaxInputCharacters = 18000);
        using var client = await factory.SignedInAsync();
        var catalog = (await client.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!;
        Assert.Equal(18000, catalog.Policy.MaxInputCharacters);
        var preview = await client.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, new string('文', 12000), "test-model"));
        preview.EnsureSuccessStatusCode(); // Escaped Chinese JSON is larger than the ordinary 64 KB body limit.
        Assert.True((await preview.Content.ReadFromJsonAsync<ContextUsageDto>())!.BudgetExceeded);
    }
}
