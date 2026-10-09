using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Chat;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class ProviderAndRecoveryTests
{
    [Fact]
    public async Task OllamaAdapterMapsNdjsonTextUsageAndServerParameters()
    {
        string? sent = null;
        using var client = new HttpClient(new FixtureHandler(async request =>
        {
            sent = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"message\":{\"content\":\"中文\"},\"done\":false}\n{\"message\":{\"content\":\"回答\"},\"done\":true,\"prompt_eval_count\":8,\"eval_count\":2}\n") };
        })) { BaseAddress = new Uri("http://fixture.test/") };
        var chunks = new List<InferenceChunk>();
        await foreach (var chunk in new OllamaProvider(client).StreamAsync("installed-model", [new("system", "系統"), new("user", "提問")], new(8192, 512, .6, "系統"), CancellationToken.None)) chunks.Add(chunk);
        Assert.Equal("中文回答", string.Concat(chunks.Select(x => x.Text)));
        Assert.True(chunks[^1].Done); Assert.Equal(8, chunks[^1].InputTokens); Assert.Equal(2, chunks[^1].OutputTokens);
        using var payload = JsonDocument.Parse(sent!);
        Assert.Equal(8192, payload.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("think").GetBoolean());
    }

    [Theory]
    [InlineData("{\"message\":{\"content\":\"partial\"},\"done\":false}\n")]
    [InlineData("{\"error\":\"private provider error\"}\n")]
    public async Task TruncatedAndErroredProviderStreamsCannotPretendCompletion(string body)
    {
        using var client = new HttpClient(new FixtureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }))) { BaseAddress = new Uri("http://fixture.test/") };
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await foreach (var chunk in new OllamaProvider(client).StreamAsync("test", [], new(8192, 512, .6, "system"), CancellationToken.None)) { } });
    }

    [Fact]
    public async Task OllamaReadsVisionMetadataAndSendsImageBytesUsingItsNativeProtocol()
    {
        using var client = new HttpClient(new FixtureHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            Assert.Equal("vision-model", json.RootElement.GetProperty("model").GetString());
            if (request.RequestUri!.AbsolutePath == "/api/show")
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"capabilities\":[\"completion\",\"vision\"]}") };
            Assert.Equal("AQID", json.RootElement.GetProperty("messages")[0].GetProperty("images")[0].GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"message\":{\"content\":\"圖片\"},\"done\":true,\"prompt_eval_count\":8,\"eval_count\":2}\n") };
        })) { BaseAddress = new Uri("http://fixture.test/") };
        var provider = new OllamaProvider(client);
        Assert.True((await provider.CapabilitiesAsync("vision-model", CancellationToken.None))!.SupportsImages);
        await foreach (var chunk in provider.StreamAsync("vision-model", [new("user", "分析圖片", [new(Guid.NewGuid(), "image/png", [1, 2, 3], 4096)])], new(8192, 512, .6, "", SupportsImages: true), CancellationToken.None)) Assert.True(chunk.Done);
    }

    [Fact]
    public async Task ModelDiscoveryUsesTheNativeInstalledCatalog()
    {
        using var client = new HttpClient(new FixtureHandler(request =>
        {
            Assert.Equal("/api/tags", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"models\":[{\"name\":\"real:8b\"}]}") });
        })) { BaseAddress = new Uri("http://fixture.test/") };
        Assert.Contains("real:8b", await new OllamaProvider(client).InstalledModelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RestartMarksInterruptedWorkFailedWithoutReissuingInference()
    {
        var interruptedId = Guid.NewGuid();
        await using var factory = new NexusFactory(db =>
        {
            var owner = new NexusUser { Sid = "S-1-5-21-test-alice", Account = "TEST\\alice", DisplayName = "alice", LastSeenAt = DateTimeOffset.UtcNow };
            var conversation = new Conversation { OwnerId = owner.Id };
            var user = new Message { ConversationId = conversation.Id, Content = "原始訊息" };
            var answer = new Message { ConversationId = conversation.Id, ParentId = user.Id, Role = "assistant", Status = RunStates.Running, Content = "既有部分回答", RunId = interruptedId };
            conversation.ActiveLeafId = answer.Id;
            var run = new GenerationRun { Id = interruptedId, OwnerId = owner.Id, ActiveOwnerId = owner.Id, ConversationId = conversation.Id, UserMessageId = user.Id, AssistantMessageId = answer.Id, ModelId = "test-model", Status = RunStates.Running, StartedAt = DateTimeOffset.UtcNow, ReservedTokens = 500, Content = answer.Content, IdempotencyKey = Guid.NewGuid().ToString(), RequestHash = new string('A', 64) };
            db.AddRange(owner, conversation, user, answer, run); db.SaveChanges();
        });
        using var client = await factory.SignedInAsync();
        var run = (await client.GetFromJsonAsync<RunDto>($"/api/v1/runs/{interruptedId}"))!;
        Assert.Equal(RunStates.Failed, run.Status); Assert.Equal("executor_lost", run.ErrorCode); Assert.Equal("既有部分回答", run.Content); Assert.Equal(0, factory.Provider.Calls); Assert.NotNull(run.Timing);
        var budget = Assert.Single((await client.GetFromJsonAsync<AiNexus.Features.Inference.EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(500, budget.ReservedTokens);
    }

    [Fact]
    public async Task CancellingQueuedWorkPreventsItsProviderRequest()
    {
        await using var factory = new NexusFactory();
        factory.Provider.DelayMs = 200; factory.Provider.NeverFinish = true;
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var a = await ChatApiTests.CreateConversation(alice); var b = await ChatApiTests.CreateConversation(bob);
        var first = await ChatApiTests.CreateRun(alice, a.Id, "A"); var second = await ChatApiTests.CreateRun(bob, b.Id, "B");
        (await bob.PostAsync($"/api/v1/runs/{second.Id}/cancel", null)).EnsureSuccessStatusCode();
        (await alice.PostAsync($"/api/v1/runs/{first.Id}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Equal(RunStates.Cancelled, (await ChatApiTests.WaitForTerminal(bob, second.Id)).Status);
        var budget = Assert.Single((await bob.GetFromJsonAsync<AiNexus.Features.Inference.EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(0, budget.ReservedTokens);
        await Task.Delay(100); Assert.True(factory.Provider.Calls <= 1);
    }

    [Fact]
    public async Task APartiallyExpiredReplayIsRecoveredAsAFullSnapshot()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var conversation = await ChatApiTests.CreateConversation(client);
        var run = await ChatApiTests.CreateRun(client, conversation.Id, "保留缺口"); var complete = await ChatApiTests.WaitForTerminal(client, run.Id);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        await db.RunEvents.Where(x => x.RunId == run.Id && x.Sequence <= 2).ExecuteDeleteAsync();
        var events = await client.GetStringAsync($"/api/v1/runs/{run.Id}/events?after=0");
        Assert.Contains("snapshot", events);
        var data = events.Split('\n').Single(x => x.StartsWith("data: ", StringComparison.Ordinal))[6..];
        Assert.Equal(complete.Content, JsonSerializer.Deserialize<RunEventDto>(data, JsonSerializerOptions.Web)!.Delta);
    }

    [Fact]
    public void SqlServerModelMatchesMigrationsAndEnforcesOwnerRunUniqueness()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);
        Assert.False(db.Database.HasPendingModelChanges());
        var run = db.Model.FindEntityType(typeof(GenerationRun))!;
        Assert.Contains(run.GetIndexes(), x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == "ActiveOwnerId");
        Assert.Contains(run.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "OwnerId", "IdempotencyKey" }));
    }

    [Fact]
    public void SubscriptionCapacityIsReleasedWhenClientsDisconnect()
    {
        var limits = new SubscriptionLimits(); var owner = Guid.NewGuid();
        using var first = limits.Acquire(owner); using (limits.Acquire(owner)) Assert.Throws<ApiException>(() => limits.Acquire(owner));
        using var replacement = limits.Acquire(owner);
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
