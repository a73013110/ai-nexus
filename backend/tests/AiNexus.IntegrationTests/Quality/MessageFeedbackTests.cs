using AiNexus.Platform.Data.Tables;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Quality.Feedback;
using AiNexus.Features.Work.Chat.Transcripts;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Quality;

public sealed class MessageFeedbackTests
{
    // 每則回答只能有一筆評分；每個請求各自一條連線，不排隊就會同時新增而撞上主鍵。
    [Fact]
    public async Task ConcurrentRatingsOfOneAnswerAllSucceed()
    {
        await using var f = new AppFactory(seed: PlatformGrants.QualityForMembers); using var owner = await f.SignedInAsync();
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "問題"); await WaitForTerminal(owner, run.Id);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => owner.PutAsJsonAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback", new FeedbackRequest(1))));

        Assert.All(responses, x => Assert.True(x.IsSuccessStatusCode));
        Assert.Single((await owner.GetFromJsonAsync<TablePage<FeedbackDto>>("/api/v1/quality/feedback"))!.Items);
    }

    [Fact]
    public async Task FeedbackIsOwnedTerminalAndPersistsInConversationWithoutPublishingText()
    {
        await using var f = new AppFactory(seed: PlatformGrants.QualityForMembers); using var owner = await f.SignedInAsync(); using var other = await f.SignedInAsync("other");
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "問題"); await WaitForTerminal(owner, run.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback", new FeedbackRequest(1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync($"/api/v1/messages/{run.UserMessageId}/feedback", new FeedbackRequest(1))).StatusCode);
        (await owner.PutAsJsonAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback", new FeedbackRequest(-1, "incomplete", "需要結論"))).EnsureSuccessStatusCode();
        Assert.Equal(-1, (await owner.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages.Single(x => x.Id == run.AssistantMessageId).FeedbackRating);
        Assert.Empty((await other.GetFromJsonAsync<TablePage<FeedbackDto>>("/api/v1/quality/feedback"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/messages/{run.AssistantMessageId}/feedback")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<TablePage<FeedbackDto>>("/api/v1/quality/feedback"))!.Items);
    }

    // 再按一次取消評分：重複移除也成功；未評分時讀取回 204，不是沒有內容的 200。
    [Fact]
    public async Task RemovingIsRepeatableAndUnratedAnswerReadsAsNoContent()
    {
        await using var f = new AppFactory(seed: PlatformGrants.QualityForMembers); using var owner = await f.SignedInAsync();
        var conversation = await CreateConversation(owner); var run = await CreateRun(owner, conversation.Id, "問題"); await WaitForTerminal(owner, run.Id);
        var url = $"/api/v1/messages/{run.AssistantMessageId}/feedback";

        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync(url)).StatusCode);
        var saved = await owner.PutAsJsonAsync(url, new FeedbackRequest(1));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(1, (await saved.Content.ReadFromJsonAsync<FeedbackDto>())!.Rating);
        Assert.Equal(1, (await owner.GetFromJsonAsync<FeedbackDto>(url))!.Rating);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(url, new FeedbackRequest(0))).StatusCode);
    }
}
