using System.Net;
using System.Net.Http.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class MultiProviderRoutingTests
{
    private static void Configure(InferenceOptions options)
    {
        options.ProviderConcurrency = new() { ["google"] = 1, ["ollama"] = 1 };
        options.Models = [
            new() { Id = "google/test-model", Provider = "google", ProviderModelId = "test-model", DisplayName = "Google test", MaxOutputTokens = 512 },
            new() { Id = "ollama/test-model", Provider = "ollama", ProviderModelId = "test-model", DisplayName = "Ollama test", MaxOutputTokens = 512 }
        ];
        options.DefaultModelId = "google/test-model";
    }

    [Fact]
    public async Task SameNativeModelRoutesToIndependentProvidersAndRunsSimultaneously()
    {
        var local = new TestProvider();
        await using var factory = new NexusFactory(inference: Configure, services: services =>
        {
            services.RemoveAllKeyed<IInferenceProvider>("ollama");
            services.AddKeyedSingleton<IInferenceProvider>("ollama", local);
        });
        factory.Provider.NeverFinish = true;
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var a = await CreateConversation(alice); var b = await CreateConversation(bob);
        var catalog = (await alice.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!;
        Assert.Equal(2, catalog.Models.Count); Assert.All(catalog.Providers!, p => Assert.True(p.Available));
        var remoteResponse = await PostRun(alice, new(a.Id, "google/test-model", "remote", null, null));
        remoteResponse.EnsureSuccessStatusCode(); var remote = (await remoteResponse.Content.ReadFromJsonAsync<RunDto>())!;
        for (var i = 0; i < 100 && factory.Provider.Calls == 0; i++) await Task.Delay(10);
        Assert.Equal(1, factory.Provider.Calls);
        var localResponse = await PostRun(bob, new(b.Id, "ollama/test-model", "local", null, null));
        localResponse.EnsureSuccessStatusCode(); var queued = (await localResponse.Content.ReadFromJsonAsync<RunDto>())!;
        var completed = await WaitForTerminal(bob, queued.Id);
        Assert.Equal("completed", completed.Status);
        Assert.Equal("running", (await alice.GetFromJsonAsync<RunDto>($"/api/v1/runs/{remote.Id}"))!.Status);
        Assert.Equal("test-model", factory.Provider.LastModel); Assert.Equal("test-model", local.LastModel);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var charge = await db.Set<ModelCharge>().SingleAsync(x => x.Id == queued.Id);
        Assert.Equal("ollama", charge.Provider); Assert.Equal("test-model", charge.ModelId);
        var spend = (await bob.GetFromJsonAsync<ConversationSpendDto>($"/api/v1/conversations/{b.Id}/spend"))!;
        Assert.Equal("Ollama test", Assert.Single(spend.Models).Label);
        (await alice.PostAsync($"/api/v1/runs/{remote.Id}/cancel", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OneProviderOutageKeepsOtherModelsUsableAndDoesNotFallbackSilently()
    {
        var local = new TestProvider { DiscoveryFail = true };
        await using var factory = new NexusFactory(inference: Configure, services: services =>
        {
            services.RemoveAllKeyed<IInferenceProvider>("ollama");
            services.AddKeyedSingleton<IInferenceProvider>("ollama", local);
        });
        using var client = await factory.SignedInAsync();
        var catalog = (await client.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!;
        Assert.True(catalog.ProviderAvailable); Assert.Equal("google/test-model", Assert.Single(catalog.Models).Id);
        Assert.False(catalog.Providers!.Single(x => x.Id == "ollama").Available);
        var conversation = await CreateConversation(client);
        var denied = await PostRun(client, new(conversation.Id, "ollama/test-model", "denied", null, null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
        Assert.Equal(0, factory.Provider.Calls); Assert.Equal(0, local.Calls);
        var accepted = await PostRun(client, new(conversation.Id, "google/test-model", "accepted", null, null));
        accepted.EnsureSuccessStatusCode(); var run = (await accepted.Content.ReadFromJsonAsync<RunDto>())!;
        Assert.Equal("completed", (await WaitForTerminal(client, run.Id)).Status);
    }

    [Fact]
    public async Task CompletedAndCancelledRunsPersistTimingsWithTheirTokens()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var client = await factory.SignedInAsync(); var conversation = await CreateConversation(client);
        var completed = await WaitForTerminal(client, (await CreateRun(client, conversation.Id, "timing")).Id);
        var timing = Assert.IsType<RunTimingDto>(completed.Timing);
        Assert.True(timing.TotalMilliseconds >= timing.GenerationMilliseconds);
        Assert.True(timing.GenerationMilliseconds > 0); Assert.True(timing.QueueMilliseconds >= 0);
        Assert.Equal(123, timing.InputTokens); Assert.Equal(6, timing.OutputTokens);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        Assert.Equal(timing, detail.Messages.Single(x => x.Id == completed.AssistantMessageId).Timing);
        var admin = (await client.GetFromJsonAsync<AiNexus.Modules.Administration.AdminConversationDetailDto>($"/api/v1/admin/conversations/{conversation.Id}"))!;
        Assert.Equal(timing, admin.Messages.Single(x => x.Id == completed.AssistantMessageId).Timing);
        var usage = (await client.GetFromJsonAsync<AiNexus.Modules.Identity.PersonalUsageDto>("/api/v1/settings/usage"))!;
        Assert.Equal(timing.TotalMilliseconds, usage.TotalDurationMilliseconds); Assert.Equal(1, usage.TimedRequests);
        factory.Provider.NeverFinish = true;
        var next = await CreateRun(client, conversation.Id, "cancel", completed.AssistantMessageId);
        var cancelled = (await (await client.PostAsync($"/api/v1/runs/{next.Id}/cancel", null)).Content.ReadFromJsonAsync<RunDto>())!;
        Assert.NotNull(cancelled.Timing); Assert.True(cancelled.Timing.TotalMilliseconds >= 0);
    }
}
