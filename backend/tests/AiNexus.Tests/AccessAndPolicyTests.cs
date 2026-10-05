using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class AccessAndPolicyTests
{
    [Fact]
    public async Task FirstLoginCreatesMemberAndResolvesGroupFeatures()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal("member", Assert.Single(me.Access.Roles).Id);
        Assert.Equal("workspace", Assert.Single(me.Access.Groups).Id);
        Assert.Contains(new FeatureDto("chat", "AI 對話", "/chat"), me.Access.Features);
        Assert.Equal(me.Access.Features.Count, me.Access.Features.Select(x => x.Id).Distinct().Count());
        (await client.GetAsync("/api/v1/models")).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("role")]
    [InlineData("group")]
    [InlineData("feature")]
    [InlineData("membership")]
    public async Task RevocationAppliesOnNextRequestAndLoginDoesNotRegrant(string target)
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            switch (target)
            {
                case "role": (await db.Set<Role>().SingleAsync(x => x.Id == "member")).Enabled = false; break;
                case "group": (await db.Set<RoleGroup>().SingleAsync(x => x.Id == "workspace")).Enabled = false; break;
                case "feature": (await db.Set<Feature>().SingleAsync(x => x.Id == "chat")).Enabled = false; break;
                case "membership": db.Set<UserRole>().RemoveRange(db.Set<UserRole>()); break;
            }
            await db.SaveChangesAsync();
        }
        // /me remains usable for account/preferences even when chat is unavailable.
        Assert.DoesNotContain((await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "chat");
        foreach (var route in new[] { "models", "conversations", $"conversations/{conversation.Id}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{route}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "hello", "test-model"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostRun(client, new(conversation.Id, "test-model", "hello", null, null))).StatusCode);
        using var newSession = await factory.SignedInAsync();
        Assert.DoesNotContain((await newSession.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "chat");
    }

    [Fact]
    public async Task MultipleRolesAndGroupsProduceUniqueFeatures()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var me = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Set<Role>().Add(new() { Id = "reviewer", Name = "覆核人員" });
            db.Set<RoleGroup>().Add(new() { Id = "review", Name = "覆核工作區" });
            db.Set<UserRole>().Add(new() { UserId = me.Id, RoleId = "reviewer" });
            db.Set<RoleGroupRole>().AddRange(new RoleGroupRole { RoleId = "reviewer", GroupId = "workspace" }, new RoleGroupRole { RoleId = "reviewer", GroupId = "review" });
            db.Set<RoleGroupFeature>().Add(new() { GroupId = "review", FeatureId = "chat" });
            await db.SaveChangesAsync();
        }
        var access = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access;
        Assert.Equal(2, access.Roles.Count);
        Assert.Equal(2, access.Groups.Count);
        Assert.Equal(access.Features.Count, access.Features.Select(x => x.Id).Distinct().Count());
        Assert.Single(access.Features, x => x.Id == "chat");
    }

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

    [Fact]
    public async Task MissingLockedDefaultNeverFallsBackToAnotherModel()
    {
        await using var factory = new NexusFactory(inference: options =>
        {
            options.AllowModelSelection = false;
            options.DefaultModelId = "missing";
            options.Models.Add(new() { Id = "missing", DisplayName = "尚未安裝" });
        });
        using var client = await factory.SignedInAsync();
        Assert.Empty((await client.GetFromJsonAsync<ModelsDto>("/api/v1/models"))!.Models);
        var conversation = await CreateConversation(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(conversation.Id, null, "hello", null, null))).StatusCode);
        Assert.Equal(0, factory.Provider.Calls);
    }

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
}
