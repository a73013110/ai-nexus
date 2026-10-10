using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration;
using AiNexus.Features.Administration.Roles;
using AiNexus.Features.Billing;
using AiNexus.Features.Chat;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Administration;

public sealed class TokenBudgetTests
{
    [Fact]
    public async Task TokenQuotaRejectsNewRequestsButKeepsIdempotentReplayAndOtherAccountsIndependent()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("基本工作區", true, ["chat"], new(["test-model"], new Dictionary<string, long> { ["test-model"] = 2200 })))).EnsureSuccessStatusCode();
        var conversation = await CreateConversation(bob); var body = new CreateRunRequest(conversation.Id, "test-model", "唯一一次", null, null); var key = Guid.NewGuid().ToString();
        var first = await PostRun(bob, body, key); first.EnsureSuccessStatusCode(); var run = (await first.Content.ReadFromJsonAsync<RunDto>())!; await WaitForTerminal(bob, run.Id);
        (await admin.PutAsJsonAsync("/api/v1/admin/groups/workspace", new GroupUpdateRequest("基本工作區", true, ["chat"], new(["test-model"], new Dictionary<string, long> { ["test-model"] = 129 })))).EnsureSuccessStatusCode();
        var replay = await PostRun(bob, body, key); replay.EnsureSuccessStatusCode(); Assert.Equal(run.Id, (await replay.Content.ReadFromJsonAsync<RunDto>())!.Id);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostRun(bob, new(conversation.Id, "test-model", "超出配額", null, null))).StatusCode);
        var ownPolicy = (await admin.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!; Assert.Equal(0, Assert.Single(ownPolicy.Models).UsedTokens);
        var usage = (await admin.GetFromJsonAsync<AdminUsageDto>("/api/v1/admin/usage"))!; Assert.Equal(1, usage.Requests); Assert.Equal(123, usage.InputTokens); Assert.Equal(2, usage.Users);
        var detail = (await bob.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!; Assert.Equal(2, detail.Messages.Count);
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
        await factory.Provider.WhenCalledAsync();
        Assert.Equal(1, factory.Provider.Calls);
        var budget = Assert.Single((await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(500, budget.ReservedTokens); Assert.Equal(0, budget.RemainingTokens);
        Assert.True(factory.Provider.LastParameters!.MaxOutputTokens < 512);
        var tasks = factory.Services.GetRequiredService<ModelTaskService>();
        var error = await tasks.GenerateAsync(owner, "transform", "短文", "改寫", CancellationToken.None);
        Assert.Equal("model_token_quota", error.Error?.Code);
        (await bob.PostAsync($"/api/v1/runs/{run.Id}/cancel", null)).EnsureSuccessStatusCode();
        budget = Assert.Single((await bob.GetFromJsonAsync<EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(500, budget.ReservedTokens); Assert.Equal(0, budget.UsedTokens);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        await db.Runs.Where(x => x.Id == run.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.CreatedAt, UtcDay.Start(DateTimeOffset.UtcNow).AddDays(-1)));
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
    public async Task TokenAndUsageWindowsUseUtcBoundariesOnEveryServerTimeZone()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync(); var owner = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var day = UtcDay.Start(DateTimeOffset.UtcNow);
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
        using var reports = factory.Services.CreateScope();
        Assert.Equal(TimeSpan.Zero, reports.ServiceProvider.GetRequiredService<UsageReports>().Since.Offset);
    }
}
