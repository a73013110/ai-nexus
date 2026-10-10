using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Quality.Feedback;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Quality;

public sealed class MessageFeedbackTests
{
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
}
