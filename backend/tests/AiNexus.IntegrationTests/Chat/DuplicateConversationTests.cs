using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class DuplicateConversationTests
{
    [Fact]
    public async Task DuplicateKeepsBranchesAndAttachmentReferencesWithNewIds()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var original = await CreateConversation(client);
        var file = await Upload(client, "notes.txt", Encoding.UTF8.GetBytes("clone attachment"));
        var run = await RunWithAttachment(client, original.Id, file.Id);
        await RunWithAttachment(client, original.Id, file.Id, run.UserMessageId);
        var copyResponse = await client.PostAsync($"/api/v1/conversations/{original.Id}/duplicate", null);
        copyResponse.EnsureSuccessStatusCode();
        var clone = (await copyResponse.Content.ReadFromJsonAsync<ConversationDto>())!;
        var cloned = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{clone.Id}"))!;
        Assert.Equal(3, cloned.Messages.Count);
        Assert.DoesNotContain(cloned.Messages, x => x.Id == run.UserMessageId);
        Assert.Equal(file.Id, Assert.Single(cloned.Messages.Single(x => x.Role == "user").Attachments!).Id);
        (await client.DeleteAsync($"/api/v1/conversations/{original.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(Encoding.UTF8.GetBytes("clone attachment"), await client.GetByteArrayAsync($"/api/v1/attachments/{file.Id}/content"));
        (await client.DeleteAsync($"/api/v1/conversations/{clone.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
        (await client.DeleteAsync($"/api/v1/files/{file.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
    }
}
