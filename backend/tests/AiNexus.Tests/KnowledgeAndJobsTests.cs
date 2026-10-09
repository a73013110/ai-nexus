using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Account;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Xunit;
using static AiNexus.Tests.ChatApiTests;
using AiNexus.Features.Jobs;

namespace AiNexus.Tests;

public sealed class KnowledgeAndJobsTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAIUlEQVR4nGP8z0AaYCJRPcOoBmIAE1GqkMCoBmIAyaEEAEAuAR9UPEsJAAAAAElFTkSuQmCC");
    private static async Task<AttachmentDto> Upload(HttpClient client, string name, byte[] bytes)
    {
        using var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(bytes), "file", name);
        using var response = await client.PostAsync("/api/v1/attachments", body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
    }
    private static async Task<CollectionDto> Collection(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("公司作業規範", "受控測試來源")); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionDto>())!;
    }
    private static async Task<DocumentDto> Document(HttpClient client, Guid collection, string text)
    {
        var file = await Upload(client, "policy.txt", Encoding.UTF8.GetBytes(text));
        using var response = await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection}/documents", new AddDocumentRequest(file.Id)); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }
    private static async Task Process(NexusFactory factory)
    {
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
    }
    [Fact]
    public async Task CollectionAclPrefiltersSearchAndOriginalsAndRevocationStopsLaterRetrieval()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var collection = await Collection(alice); var document = await Document(alice, collection.Resource.Id, "Only approved staff may view this company policy."); await Process(factory);
        var request = new KnowledgeSearchRequest("company policy", [collection.Resource.Id]);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/knowledge/search", request)).StatusCode);
        var calls = factory.Embeddings.Calls;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/documents/{document.Id}/content")).StatusCode);
        Assert.Equal(calls, factory.Embeddings.Calls);
        var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        (await alice.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/access", new ResourceAclRequest([new(user.Id, "viewer")], []))).EnsureSuccessStatusCode();
        var result = await bob.PostAsJsonAsync("/api/v1/knowledge/search", request); result.EnsureSuccessStatusCode();
        Assert.Single((await result.Content.ReadFromJsonAsync<KnowledgeSearchDto>())!.Hits);
        Assert.Contains("approved staff", Encoding.UTF8.GetString(await bob.GetByteArrayAsync($"/api/v1/documents/{document.Id}/content")));
        var sharedJob = (await bob.GetFromJsonAsync<DocumentJobDto>($"/api/v1/documents/{document.Id}/job"))!;
        Assert.False(sharedJob.CanControl); Assert.Equal("completed", sharedJob.Job.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/jobs/{document.JobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"/api/v1/documents/{document.Id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/access", new ResourceAclRequest([], []))).EnsureSuccessStatusCode();
        calls = factory.Embeddings.Calls;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/knowledge/search", request)).StatusCode);
        Assert.Equal(calls, factory.Embeddings.Calls);
    }
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
        var collection = await Collection(client);
        var retained = await Upload(client, "retained.txt", Encoding.UTF8.GetBytes("Knowledge source to retain."));
        (await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/documents", new AddDocumentRequest(retained.Id))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/attachments/{retained.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/files/{retained.Id}")).StatusCode);
    }
    [Fact]
    public async Task DeletingCollectionPurgesIndexAndUnlinksSourcesButRetainsLibraryOriginal()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var collection = await Collection(client);
        var doc = await Document(client, collection.Resource.Id, "Content removed with the collection."); await Process(factory);
        var conversation = await CreateConversation(client);
        (await client.PutAsJsonAsync($"/api/v1/conversations/{conversation.Id}/knowledge", new KnowledgeSelectionDto([collection.Resource.Id]))).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<KnowledgeSelectionDto>($"/api/v1/conversations/{conversation.Id}/knowledge"))!.CollectionIds);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/{doc.Id}/content")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.False(await db.Set<DocumentPage>().AnyAsync()); Assert.False(await db.Set<KnowledgeChunk>().AnyAsync());
        var file = Assert.Single(await db.Set<Attachment>().ToListAsync()); Assert.True(file.InLibrary);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
    }
    [Fact]
    public async Task RetrievedSourceIsSnapshottedWithPageCitationForGeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var collection = await Collection(client); var document = await Document(client, collection.Resource.Id, "Travel budget approval requires manager signature."); await Process(factory);
        var conversation = await CreateConversation(client);
        (await client.PutAsJsonAsync($"/api/v1/conversations/{conversation.Id}/knowledge", new KnowledgeSelectionDto([collection.Resource.Id]))).EnsureSuccessStatusCode();
        var run = await CreateRun(client, conversation.Id, "travel budget approval"); await WaitForTerminal(client, run.Id);
        Assert.Contains("manager signature", factory.Provider.LastParameters!.SystemPrompt);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        var source = Assert.Single(detail.Messages.Single(x => x.Role == "assistant").Sources!);
        Assert.Equal(document.Id, source.DocumentId); Assert.Equal(1, source.PageNumber);
    }
    [Fact]
    public async Task FailedIndexResumesPersistedPagesAndExpiredLeaseCannotDoubleProcess()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var collection = await Collection(client);
        var document = await Document(client, collection.Resource.Id, new string('文', 1300));
        factory.Embeddings.Fail = true; await Process(factory);
        document = (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{document.Id}"))!;
        var failed = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!;
        Assert.Equal("failed", failed.Status); Assert.Equal("fixture_embedding_failed", failed.ErrorCode);
        Assert.Single((await client.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{document.Id}/pages"))!);
        factory.Embeddings.Fail = false;
        (await client.PostAsync($"/api/v1/jobs/{document.JobId}/retry", null)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var job = await db.Set<BackgroundJob>().SingleAsync(x => x.Id == document.JobId); job.Status = "running"; job.LeaseToken = Guid.NewGuid(); job.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        using var first = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        using var second = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        await Task.WhenAll(first.ProcessNextAsync(CancellationToken.None), second.ProcessNextAsync(CancellationToken.None));
        var done = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!;
        Assert.Equal("completed", done.Status); Assert.Equal(2, done.Attempt);
        Assert.Equal(2, (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{document.Id}"))!.ChunkCount);
        Assert.Equal(2, factory.Embeddings.Calls); // one failed batch and one resumed batch
    }
    [Fact]
    public async Task JobsAreOwnerScopedCancellationIsDurableAndRetryRequiresCsrf()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var collection = await Collection(alice); var document = await Document(alice, collection.Resource.Id, "Cancellation sample document.");
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/jobs/{document.JobId}/cancel", null)).StatusCode);
        (await alice.PostAsync($"/api/v1/jobs/{document.JobId}/cancel", null)).EnsureSuccessStatusCode(); await Process(factory);
        Assert.Equal("cancelled", (await alice.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!.Status);
        Assert.Equal(0, factory.Embeddings.Calls);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsync($"/api/v1/jobs/{document.JobId}/retry", null)).StatusCode);
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
    [Fact]
    public async Task ScannedPdfUsesVisionOcrBeforeItCanEnterChatContext()
    {
        await using var factory = new NexusFactory(inference: x => x.Models[0].SupportsImages = true);
        using var client = await factory.SignedInAsync();
        var builder = new PdfDocumentBuilder(); builder.AddPage(300, 400).AddPng(Png, new PdfRectangle(0, 0, 300, 400));
        var file = await Upload(client, "scan.pdf", builder.Build()); Assert.Equal("ocr-required", file.AnalysisMode);
        var conversation = await CreateConversation(client);
        Assert.Equal(HttpStatusCode.Conflict, (await PostRun(client, new(conversation.Id, "test-model", "Read", null, null, AttachmentIds: [file.Id]))).StatusCode);
        var response = await client.PostAsync($"/api/v1/attachments/{file.Id}/document", null); response.EnsureSuccessStatusCode(); var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        await Process(factory);
        Assert.Equal("ready", (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{document.Id}"))!.Status);
        var page = Assert.Single((await client.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{document.Id}/pages"))!); Assert.Equal("ocr", page.Extraction); Assert.True(page.NeedsReview);
        Assert.Equal(["system", "user"], factory.Provider.LastMessages.Select(x => x.Role));
        Assert.Single(factory.Provider.LastMessages.Single(x => x.Role == "user").Images!);
        Assert.Equal("extracted-text", (await client.GetFromJsonAsync<AttachmentDto>($"/api/v1/attachments/{file.Id}"))!.AnalysisMode);
    }
}
