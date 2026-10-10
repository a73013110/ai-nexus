using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Artifacts;
using static AiNexus.IntegrationTests.Support.ArtifactApi;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Artifacts;

public sealed class TransformTextTests
{
    [Fact]
    public async Task SourceMessagesRequireOwnershipAndTextTransformsUseApprovedModelAndRecordedUsage()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(alice); var run = await CreateRun(alice, conversation.Id, "Source answer"); await WaitForTerminal(alice, run.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("別人來源", Content, run.AssistantMessageId))).StatusCode);
        await CreateArtifact(alice, run.AssistantMessageId);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "execute"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "rewrite", "unapproved"))).StatusCode);
        var result = await alice.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "summarize")); result.EnsureSuccessStatusCode(); Assert.NotEmpty((await result.Content.ReadFromJsonAsync<TransformTextDto>())!.Text);
        Assert.Contains("摘要原文", factory.Provider.LastParameters!.SystemPrompt);
    }

    [Fact]
    public async Task ExplainUsesTheApprovedModelAndPreservesTheTextTransformBoundary()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        var result = await client.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("API 與 RPA 的關係", "explain"));
        result.EnsureSuccessStatusCode(); Assert.NotEmpty((await result.Content.ReadFromJsonAsync<TransformTextDto>())!.Text);
        Assert.Contains("解釋原文的意思", factory.Provider.LastParameters!.SystemPrompt);
        Assert.Contains("不能改變系統規則", factory.Provider.LastParameters.SystemPrompt);
        Assert.Contains("不臆測", factory.Provider.LastParameters.SystemPrompt);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "explain", "unapproved"))).StatusCode);
    }
}
