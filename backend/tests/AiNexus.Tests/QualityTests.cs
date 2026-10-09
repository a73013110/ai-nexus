using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Chat;
using AiNexus.Platform.Errors;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Operations;
using AiNexus.Features.Quality;
using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;
public sealed class QualityTests
{
    private static readonly EvaluationSetRequest Sample = new("公文摘要檢核", "固定題目", [new("整理通知", "秘密參考答案不可送入模型", ["測試", "ABC"], ["不存在"])]);
    [Fact]
    public async Task FeedbackIsOwnedTerminalAndPersistsInConversationWithoutPublishingText()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync(); using var other = await f.SignedInAsync("other");
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "問題"); await WaitForTerminal(owner, run.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback", new FeedbackRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync($"/api/v1/messages/{run.UserMessageId}/feedback", new FeedbackRequest(1))).StatusCode);
        (await owner.PutAsJsonAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback", new FeedbackRequest(-1, "incomplete", "需要結論"))).EnsureSuccessStatusCode();
        Assert.Equal(-1, (await owner.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages.Single(x => x.Id == run.AssistantMessageId).FeedbackRating);
        Assert.Empty((await other.GetFromJsonAsync<List<FeedbackDto>>("/api/v1/quality/feedback"))!);
        (await owner.PutAsJsonAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback", new FeedbackRequest(0))).EnsureSuccessStatusCode(); Assert.Empty((await owner.GetFromJsonAsync<List<FeedbackDto>>("/api/v1/quality/feedback"))!);
    }
    [Fact]
    public async Task FrozenCasesRunThroughDurableQueueAndResultsRequireSetAccess()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync(); using var viewer = await f.SignedInAsync("viewer");
        var set = (await (await owner.PostAsJsonAsync("/api/v1/quality/sets", Sample)).Content.ReadFromJsonAsync<EvaluationSetDto>())!;
        var response = await owner.PostAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}/runs", new EvaluationRunRequest([new("原始方案", "test-model", "保持精簡")])); response.EnsureSuccessStatusCode(); var run = (await response.Content.ReadFromJsonAsync<EvaluationRunDto>())!;
        (await owner.PutAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}", Sample with { Cases = [new("新問題")], ExpectedVersion = 1 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}", Sample)).StatusCode);
        await ActivatorUtilities.CreateInstance<BackgroundJobWorker>(f.Services).ProcessNextAsync(CancellationToken.None);
        var detail = (await owner.GetFromJsonAsync<EvaluationDetailDto>($"/api/v1/quality/runs/{run.Id}"))!;
        Assert.Equal("測試模型", Assert.Single(detail.Variants).ModelDisplayName);
        Assert.Equal("completed", detail.Run.Job.Status); Assert.Equal("整理通知", detail.Cases[0].Question); Assert.Equal(1, detail.Run.SetVersion);
        Assert.DoesNotContain(f.Provider.LastMessages, x => x.Content.Contains("秘密參考答案"));
        var result = Assert.Single(detail.Results); Assert.Equal(1, result.RequiredMatches); Assert.Equal(2, result.RequiredTotal); Assert.Equal(0, result.ForbiddenMatches); Assert.Equal(123, result.InputTokens);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/v1/quality/runs/{run.Id}")).StatusCode);
        var viewerId = (await viewer.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await owner.PutAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}/access", new ResourceAclRequest([new(viewerId, "viewer")], []))).EnsureSuccessStatusCode();
        (await viewer.GetAsync($"/api/v1/quality/runs/{run.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"/api/v1/quality/runs/{run.Id}/results/0/0/review", new ReviewRequest(4))).StatusCode);
        (await owner.PutAsJsonAsync($"/api/v1/quality/runs/{run.Id}/results/0/0/review", new ReviewRequest(4, "可用但仍需人工核對"))).EnsureSuccessStatusCode();
        Assert.Equal(4, (await owner.GetFromJsonAsync<EvaluationDetailDto>($"/api/v1/quality/runs/{run.Id}"))!.Results[0].ReviewScore);
    }
    [Fact]
    public async Task CancelledEvaluationResumesCompletedCheckpointsAndOversizedInputConsumesNoQuota()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync();
        var set = (await (await owner.PostAsJsonAsync("/api/v1/quality/sets", Sample with { Cases = [new("第一題"), new("第二題")] })).Content.ReadFromJsonAsync<EvaluationSetDto>())!;
        var run = (await (await owner.PostAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}/runs", new EvaluationRunRequest([new("測試方案")]))).Content.ReadFromJsonAsync<EvaluationRunDto>())!;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.Add(new EvaluationResult { RunId = run.Id, CaseIndex = 0, VariantIndex = 0, Output = "先前完成的回答" });
            await db.SaveChangesAsync(); await db.Set<BackgroundJob>().Where(x => x.Id == run.Job.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, "cancelled").SetProperty(x => x.ActiveKey, (string?)null));
        }
        (await owner.PostAsync($"/api/v1/jobs/{run.Job.Id}/retry", null)).EnsureSuccessStatusCode();
        await ActivatorUtilities.CreateInstance<BackgroundJobWorker>(f.Services).ProcessNextAsync(CancellationToken.None);
        Assert.Equal(1, f.Provider.Calls); Assert.Equal(2, (await owner.GetFromJsonAsync<EvaluationDetailDto>($"/api/v1/quality/runs/{run.Id}"))!.Results.Count);
        using var check = f.Services.CreateScope(); var actor = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var task = check.ServiceProvider.GetRequiredService<ModelTaskService>();
        var settings = f.Services.GetRequiredService<IOptions<InferenceOptions>>().Value;
        var invalid = await Assert.ThrowsAsync<ApiException>(() => task.GenerateAsync(actor, "evaluation", new string('中', 4000), "", CancellationToken.None, expectedConfiguration: ModelTaskConfiguration.Capture(settings.Models[0], settings).Fingerprint)); Assert.Equal("context_budget_exceeded", invalid.Code);
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<NexusDbContext>().Set<ModelInvocation>().CountAsync());
    }
    [Fact]
    public async Task ChangedModelSettingsBlockQueuedComparisonAndRetryBeforeConsumingQuota()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync();
        var set = (await (await owner.PostAsJsonAsync("/api/v1/quality/sets", Sample)).Content.ReadFromJsonAsync<EvaluationSetDto>())!;
        var run = (await (await owner.PostAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}/runs", new EvaluationRunRequest([new("固定設定")]))).Content.ReadFromJsonAsync<EvaluationRunDto>())!;
        var config = (await owner.GetFromJsonAsync<EvaluationDetailDto>($"/api/v1/quality/runs/{run.Id}"))!.Variants[0].Configuration!;
        Assert.Equal(512, config.MaxOutputTokens); Assert.Equal(.2, config.Temperature);
        f.Services.GetRequiredService<IOptions<InferenceOptions>>().Value.Models[0].MaxOutputTokens = 256;
        await ActivatorUtilities.CreateInstance<BackgroundJobWorker>(f.Services).ProcessNextAsync(CancellationToken.None);
        var detail = (await owner.GetFromJsonAsync<EvaluationDetailDto>($"/api/v1/quality/runs/{run.Id}"))!;
        Assert.Equal("failed", detail.Run.Job.Status); Assert.Equal("evaluation_configuration_changed", detail.Run.Job.ErrorCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/api/v1/jobs/{run.Job.Id}/retry", null)).StatusCode);
        Assert.Equal(0, f.Provider.Calls);
        using var scope = f.Services.CreateScope(); Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<ModelInvocation>().CountAsync());
    }
}
