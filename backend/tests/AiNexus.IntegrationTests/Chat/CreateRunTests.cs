using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class CreateRunTests
{
    [Fact]
    public async Task ReasoningIsValidatedAndSnapshottedForTheWorker()
    {
        await using var factory = new NexusFactory(inference: options =>
        {
            options.Models[0].ReasoningControl = "google-level";
            options.Models[0].ReasoningEfforts = ["minimal", "high"];
            options.Models[0].DefaultReasoningEffort = "minimal";
        });
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var invalid = await PostRun(client, new(conversation.Id, "test-model", "hello", null, null, "medium"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("reasoning_not_supported", await invalid.Content.ReadAsStringAsync());
        var accepted = await PostRun(client, new(conversation.Id, "test-model", "hello", null, null, "high"));
        accepted.EnsureSuccessStatusCode();
        var run = (await accepted.Content.ReadFromJsonAsync<RunDto>())!;
        await WaitForTerminal(client, run.Id);
        Assert.Equal("high", factory.Provider.LastParameters!.ReasoningEffort);
        Assert.Equal("google-level", factory.Provider.LastParameters.ReasoningControl);
        using var scope = factory.Services.CreateScope();
        var json = (await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Runs.SingleAsync()).ParametersJson;
        Assert.Equal("high", JsonSerializer.Deserialize<GenerationParameters>(json)!.ReasoningEffort);
    }

    [Fact]
    public async Task IdempotencyDoesNotCreateAnotherMessageOrGeneration()
    {
        await using var factory = new NexusFactory();
        factory.Provider.DelayMs = 100;
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var key = Guid.NewGuid().ToString();
        var request = new CreateRunRequest(conversation.Id, "test-model", "你好", null, null);
        var first = await PostRun(client, request, key);
        first.EnsureSuccessStatusCode();
        var original = (await first.Content.ReadFromJsonAsync<RunDto>())!;
        var retry = await PostRun(client, request, key);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(original.Id, (await retry.Content.ReadFromJsonAsync<RunDto>())!.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await PostRun(client, request with { Prompt = "changed" }, key)).StatusCode);
        await WaitForTerminal(client, original.Id);
        Assert.Equal(1, factory.Provider.Calls);
        Assert.Equal(2, (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages.Count);
    }

    [Fact]
    public async Task MultiturnContextComesFromTheSelectedBranchAndEditsPreserveHistory()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var first = await CreateRun(client, conversation.Id, "原始提問");
        await WaitForTerminal(client, first.Id);
        var second = await CreateRun(client, conversation.Id, "追問", first.AssistantMessageId);
        await WaitForTerminal(client, second.Id);
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, factory.Provider.LastMessages.Select(x => x.Role));
        var edited = await CreateRun(client, conversation.Id, "修改過的提問");
        await WaitForTerminal(client, edited.Id);
        Assert.DoesNotContain(factory.Provider.LastMessages, x => x.Content == "原始提問");
        var regeneratedResponse = await PostRun(client, new(conversation.Id, "test-model", null, null, first.UserMessageId));
        regeneratedResponse.EnsureSuccessStatusCode();
        var regenerated = (await regeneratedResponse.Content.ReadFromJsonAsync<RunDto>())!;
        await WaitForTerminal(client, regenerated.Id);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        Assert.Equal(7, detail.Messages.Count);
        Assert.Equal(2, detail.Messages.Count(x => x.ParentId == first.UserMessageId));
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/branch", new SelectBranchRequest(second.AssistantMessageId))).EnsureSuccessStatusCode();
        Assert.Equal(second.AssistantMessageId, (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Conversation.ActiveLeafId);
    }

    [Fact]
    public async Task CrossConversationParentAndOversizedContextAreRejectedAtomically()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var first = await CreateConversation(client);
        var second = await CreateConversation(client);
        var run = await CreateRun(client, first.Id, "你好");
        await WaitForTerminal(client, run.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(second.Id, "test-model", "篡改上文", run.AssistantMessageId, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(second.Id, "test-model", new string('中', 4000), null, null))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{second.Id}"))!.Messages);
    }
}
