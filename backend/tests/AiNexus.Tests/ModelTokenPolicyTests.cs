using System.Net;
using System.Net.Http.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Administration;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class ModelTokenPolicyTests
{
    [Fact]
    public async Task PersonalOverridesArePerModelAndGroupGrantsRemainAnIntersection()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], inference: options => options.Models.Add(new() { Id = "not-approved", Provider = "google", ProviderModelId = "not-approved", DisplayName = "另一模型" }));
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("工作區", true, ["chat"], new(["test-model"], new Dictionary<string, long> { ["test-model"] = 1000, ["not-approved"] = 500 })))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest(["test-model", "not-approved"], new Dictionary<string, long> { ["test-model"] = 2000 }))).EnsureSuccessStatusCode();
        var policy = (await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Equal("test-model", Assert.Single(policy.AllowedModelIds!));
        Assert.Equal(2000, policy.Models.Single(x => x.ModelId == "test-model").DailyTokenLimit);
        Assert.Equal("personal", policy.Models.Single(x => x.ModelId == "test-model").Source);
        Assert.Equal(500, policy.Models.Single(x => x.ModelId == "not-approved").DailyTokenLimit);
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest())).EnsureSuccessStatusCode();
        policy = (await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        Assert.Equal(1000, policy.Models.Single(x => x.ModelId == "test-model").DailyTokenLimit);
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit"))!;
        Assert.Contains(audit, x => x.Action == "admin.user_model_policy" && x.DetailsJson!.Contains("2000"));
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest())).StatusCode);
    }

    [Fact]
    public async Task ActiveAndUnreportedRequestsReserveTokensAcrossChatAndModelTasks()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var owner = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest(DailyTokenLimits: new Dictionary<string, long> { ["test-model"] = 500 }))).EnsureSuccessStatusCode();
        factory.Provider.NeverFinish = true;
        var conversation = await CreateConversation(bob); var run = await CreateRun(bob, conversation.Id, "保留額度");
        for (var i = 0; factory.Provider.Calls == 0 && i < 100; i++) await Task.Delay(20);
        Assert.Equal(1, factory.Provider.Calls);
        var budget = Assert.Single((await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(500, budget.ReservedTokens); Assert.Equal(0, budget.RemainingTokens);
        Assert.True(factory.Provider.LastParameters!.MaxOutputTokens < 512);
        var tasks = factory.Services.GetRequiredService<ModelTaskService>();
        var error = await Assert.ThrowsAsync<ApiException>(() => tasks.GenerateAsync(owner, "transform", "短文", "改寫", CancellationToken.None));
        Assert.Equal("model_token_quota", error.Code);
        (await bob.PostAsync($"/api/v1/runs/{run.Id}/cancel", null)).EnsureSuccessStatusCode();
        budget = Assert.Single((await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(500, budget.ReservedTokens); Assert.Equal(0, budget.UsedTokens);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        await db.Runs.Where(x => x.Id == run.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, UtcDay.Today.AddDays(-1)));
        budget = Assert.Single((await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(0, budget.ReservedTokens); Assert.Equal(500, budget.RemainingTokens);
    }

    [Fact]
    public async Task EachModelCountsReportedUsageAndIgnoresEmbeddingAndWebSearch()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.AddRange(new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "completed", InputTokens = 300, OutputTokens = 40, ReservedTokens = 1500 },
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "embedding", Status = "completed", InputTokens = 90000, OutputTokens = 0 },
                new ModelInvocation { OwnerId = owner, ModelId = "web-search", Kind = "web-search", Status = "completed" });
            await db.SaveChangesAsync();
        }
        var budget = Assert.Single((await admin.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(340, budget.UsedTokens); Assert.Equal(0, budget.ReservedTokens);
        foreach (var limits in new[] { new Dictionary<string, long> { ["test-model"] = -1 }, new Dictionary<string, long> { ["unknown"] = 100 }, new Dictionary<string, long> { ["test-model"] = ModelPolicyService.MaximumTokenLimit + 1 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest(DailyTokenLimits: limits))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new { dailyTokenLimits = new Dictionary<string, double> { ["test-model"] = 1.5 } })).StatusCode);
    }

    [Fact]
    public async Task FileLibraryIsRegisteredAndRevocationBlocksItsApi()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        Assert.Contains((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "files" && x.Name == "檔案庫");
        (await admin.GetAsync("/api/v1/files")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/files", new FeatureUpdateRequest("檔案庫", 15, false))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/files")).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "files");
    }

    [Fact]
    public async Task TokenAndUsageWindowsUseUtcBoundariesOnEveryServerTimeZone()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var day = UtcDay.Today;
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{owner}/model-policy", new ModelPolicyRequest(DailyTokenLimits: new Dictionary<string, long> { ["test-model"] = 500 }))).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.AddRange(
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "completed", CreatedAt = day.AddTicks(-1), InputTokens = 300, OutputTokens = 40 },
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "running", CreatedAt = day.AddTicks(-1), ReservedTokens = 1500 },
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "completed", CreatedAt = day, InputTokens = 10, OutputTokens = 2 },
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "running", CreatedAt = day, ReservedTokens = 400 },
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "completed", CreatedAt = day.AddDays(-29).AddTicks(-1), InputTokens = 90, OutputTokens = 0 },
                new ModelInvocation { OwnerId = owner, ModelId = "test-model", Kind = "transform", Status = "completed", CreatedAt = day.AddDays(-29), InputTokens = 80, OutputTokens = 0 });
            await db.SaveChangesAsync();
            var totals = await scope.ServiceProvider.GetRequiredService<UsageReports>().AllAsync(CancellationToken.None);
            Assert.Equal(5, totals.Requests); Assert.Equal(390, totals.InputTokens); Assert.Equal(42, totals.OutputTokens);
        }
        var policy = (await admin.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!;
        var budget = Assert.Single(policy.Models);
        Assert.Equal(12, budget.UsedTokens); Assert.Equal(400, budget.ReservedTokens); Assert.Equal(88, budget.RemainingTokens);
        Assert.Equal(TimeSpan.Zero, policy.ResetsAt.Offset); Assert.Equal(day.AddDays(1), policy.ResetsAt);
        Assert.Equal(TimeSpan.Zero, UsageReports.Since.Offset);
    }
}
