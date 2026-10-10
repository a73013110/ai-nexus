using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Inference;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class KnowledgeDocumentTests
{
    [Fact]
    public async Task RemovingDraftAlsoRemovesItsPrivateReaderButRetainsCollectionReferences()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var attachment = await Upload(client, "draft.txt", Encoding.UTF8.GetBytes("Private draft to discard."));
        var reader = (await (await client.PostAsync($"/api/v1/attachments/{attachment.Id}/document", null)).Content.ReadFromJsonAsync<DocumentDto>())!;
        (await client.DeleteAsync($"/api/v1/attachments/{attachment.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/{reader.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/attachments/{attachment.Id}")).StatusCode);
        Assert.True((await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{reader.JobId}"))!.CancelRequested);
        var collection = await CreateCollection(client);
        var retained = await Upload(client, "retained.txt", Encoding.UTF8.GetBytes("Knowledge source to retain."));
        (await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/documents", new AddDocumentRequest(retained.Id))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/attachments/{retained.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/files/{retained.Id}")).StatusCode);
    }

    [Fact]
    public async Task ReaderPinsOriginalAfterConversationDeletionAndOwnerDeleteReclaimsIt()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var file = await Upload(client, "retained.txt", Encoding.UTF8.GetBytes("A document retained independently of chat history."));
        var response = await client.PostAsync($"/api/v1/attachments/{file.Id}/document", null); response.EnsureSuccessStatusCode(); var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        await Process(factory); var conversation = await CreateConversation(client);
        var runResponse = await PostRun(client, new(conversation.Id, "test-model", "Read attachment", null, null, AttachmentIds: [file.Id])); runResponse.EnsureSuccessStatusCode();
        await WaitForTerminal(client, (await runResponse.Content.ReadFromJsonAsync<RunDto>())!.Id);
        (await client.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
        Assert.NotEmpty(await client.GetByteArrayAsync($"/api/v1/documents/{document.Id}/content"));
        (await client.DeleteAsync($"/api/v1/documents/{document.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
        (await client.DeleteAsync($"/api/v1/files/{file.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
    }
}
