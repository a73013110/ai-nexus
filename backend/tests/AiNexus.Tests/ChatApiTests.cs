using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class ChatApiTests
{
    [Fact]
    public async Task AnonymousAndUntrustedIdentityHeaderAreRejected()
    {
        await using var factory = new NexusFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User", "administrator");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/conversations")).StatusCode);
    }

    [Fact]
    public async Task WritesRequireCsrfBoundToTheSignedInUser()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var token = alice.DefaultRequestHeaders.GetValues("X-Nexus-CSRF").Single();
        bob.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        bob.DefaultRequestHeaders.Add("X-Nexus-CSRF", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest())).StatusCode);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest())).StatusCode);
    }

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

    [Fact]
    public async Task CatalogOnlyContainsInstalledApprovedModels()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var catalog = await client.GetFromJsonAsync<ModelsDto>("/api/v1/models");
        Assert.Equal("test-model", Assert.Single(catalog!.Models).Id);
        var conversation = await CreateConversation(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(conversation.Id, "not-approved", "hello", null, null))).StatusCode);
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

    [Fact]
    public async Task QueueIsBoundedAndOnlyOneProviderCallRunsAtATime()
    {
        await using var factory = new NexusFactory();
        factory.Provider.DelayMs = 200;
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
        await WaitForTerminal(alice, first.Id);
        await WaitForTerminal(bob, second.Id);
        Assert.Equal(1, factory.Provider.MaxConcurrent);
    }

    [Fact]
    public async Task CancelPreservesPartialOutputAndReplayHasIncreasingSequences()
    {
        await using var factory = new NexusFactory();
        factory.Provider.DelayMs = 150;
        factory.Provider.NeverFinish = true;
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var run = await CreateRun(client, conversation.Id, "取消測試");
        RunDto current = run;
        for (var i = 0; i < 100 && current.Content.Length == 0; i++) { await Task.Delay(20); current = (await client.GetFromJsonAsync<RunDto>($"/api/v1/runs/{run.Id}"))!; }
        Assert.NotEmpty(current.Content);
        var response = await client.PostAsync($"/api/v1/runs/{run.Id}/cancel", null);
        response.EnsureSuccessStatusCode();
        var cancelled = (await response.Content.ReadFromJsonAsync<RunDto>())!;
        Assert.Equal(RunStates.Cancelled, cancelled.Status);
        Assert.NotEmpty(cancelled.Content);
        var events = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after=0");
        var ids = events.Split('\n').Where(x => x.StartsWith("id: ")).Select(x => long.Parse(x[4..])).ToList();
        Assert.Equal(ids.Distinct().Order(), ids);
        Assert.Contains("cancelled", events);
        var resumed = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after={ids[0]}");
        Assert.DoesNotContain($"id: {ids[0]}\n", resumed);
    }

    [Fact]
    public async Task ProviderFailureAndTimeoutBecomeTerminalAndAllowRetry()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        factory.Provider.Fail = true;
        var first = await CreateRun(client, conversation.Id, "失敗");
        var failure = await WaitForTerminal(client, first.Id);
        Assert.Equal("provider_connection_lost", failure.ErrorCode);
        factory.Provider.Fail = false;
        factory.Provider.NeverFinish = true;
        var second = await CreateRun(client, conversation.Id, "逾時");
        var timedOut = await WaitForTerminal(client, second.Id);
        Assert.Equal("generation_timeout", timedOut.ErrorCode);
        Assert.NotEmpty(timedOut.Content);
    }

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
        var data = events.Split('\n').Single(x => x.StartsWith("data: "))[6..];
        Assert.Equal(completed.Content, JsonSerializer.Deserialize<RunEventDto>(data, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Delta);
    }

    internal static async Task<ConversationDto> CreateConversation(HttpClient client, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest(title));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }
    internal static async Task<HttpResponseMessage> PostRun(HttpClient client, CreateRunRequest request, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/runs") { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(message);
    }
    internal static async Task<RunDto> CreateRun(HttpClient client, Guid conversation, string prompt, Guid? parent = null)
    {
        var response = await PostRun(client, new(conversation, "test-model", prompt, parent, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunDto>())!;
    }
    internal static async Task<RunDto> WaitForTerminal(HttpClient client, Guid id)
    {
        for (var i = 0; i < 400; i++)
        {
            var run = (await client.GetFromJsonAsync<RunDto>($"/api/v1/runs/{id}"))!;
            if (!RunStates.IsActive(run.Status)) return run;
            await Task.Delay(20);
        }
        throw new TimeoutException("Run did not finish.");
    }
}
