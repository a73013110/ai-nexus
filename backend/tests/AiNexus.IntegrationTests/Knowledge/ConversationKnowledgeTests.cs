using System.Net.Http.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Knowledge.Collections;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class ConversationKnowledgeTests
{
    [Fact]
    public async Task RetrievedSourceIsSnapshottedWithPageCitationForGeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var collection = await CreateCollection(client); var document = await AddDocument(client, collection.Resource.Id, "Travel budget approval requires manager signature."); await Process(factory);
        var conversation = await CreateConversation(client);
        (await client.PutAsJsonAsync($"/api/v1/conversations/{conversation.Id}/knowledge", new KnowledgeSelectionDto([collection.Resource.Id]))).EnsureSuccessStatusCode();
        var run = await CreateRun(client, conversation.Id, "travel budget approval"); await WaitForTerminal(client, run.Id);
        Assert.Contains("manager signature", factory.Provider.LastParameters!.SystemPrompt);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        var source = Assert.Single(detail.Messages.Single(x => x.Role == "assistant").Sources!);
        Assert.Equal(document.Id, source.DocumentId); Assert.Equal(1, source.PageNumber);
    }
}
