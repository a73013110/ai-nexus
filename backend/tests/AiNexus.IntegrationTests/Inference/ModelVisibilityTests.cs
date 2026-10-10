using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Inference;

public sealed class ModelVisibilityTests
{
    [Fact]
    public async Task LockedHiddenModelCannotBeChangedOrDisclosedInResponses()
    {
        await using var factory = new NexusFactory(inference: options =>
        {
            options.AllowModelSelection = false;
            options.ShowModelNames = false;
            options.DefaultModelId = "test-model";
            options.Models.Add(new() { Id = "not-approved", DisplayName = "第二個真實名稱" });
        });
        using var client = await factory.SignedInAsync();
        var catalog = (await client.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!;
        Assert.False(catalog.Policy.AllowModelSelection);
        Assert.False(catalog.Policy.ShowModelNames);
        Assert.Equal("model-1", Assert.Single(catalog.Models).Id);
        var conversation = await CreateConversation(client);
        var denied = await PostRun(client, new(conversation.Id, "model-2", "切換模型", null, null));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Contains("model_selection_disabled", await denied.Content.ReadAsStringAsync());
        var accepted = await PostRun(client, new(conversation.Id, null, "隱藏模型測試", null, null));
        accepted.EnsureSuccessStatusCode();
        var run = (await accepted.Content.ReadFromJsonAsync<RunDto>())!;
        await WaitForTerminal(client, run.Id);
        Assert.Equal("test-model", factory.Provider.LastModel);
        foreach (var path in new[] { "models", "me", $"runs/{run.Id}", $"conversations/{conversation.Id}" })
            Assert.DoesNotContain("test-model", await client.GetStringAsync($"/api/v1/{path}"));
        var cancelled = await client.PostAsync($"/api/v1/runs/{run.Id}/cancel", null);
        Assert.DoesNotContain("test-model", await cancelled.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Equal("test-model", (await db.Runs.SingleAsync()).ModelId);
    }

    [Fact]
    public async Task HiddenSelectableModelPreferenceStoresProviderIdAndReturnsAlias()
    {
        await using var factory = new NexusFactory(inference: options => options.ShowModelNames = false);
        using var client = await factory.SignedInAsync();
        var saved = await client.PutAsJsonAsync("/api/v1/preferences", new PreferencesDto("dark", true, "model-1"));
        saved.EnsureSuccessStatusCode();
        Assert.Equal("model-1", (await saved.Content.ReadFromJsonAsync<PreferencesDto>())!.DefaultModelId);
        Assert.DoesNotContain("test-model", await client.GetStringAsync("/api/v1/me"));
        using var scope = factory.Services.CreateScope();
        Assert.Equal("test-model", (await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Users.SingleAsync()).Preferences.DefaultModelId);
    }
}
