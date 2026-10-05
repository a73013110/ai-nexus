using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Knowledge;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class FileLibraryTests
{
    private static async Task<AttachmentDto> Upload(HttpClient client, string name, string text)
    {
        using var body = new MultipartFormDataContent(); body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "file", name);
        using var response = await client.PostAsync("/api/v1/attachments", body); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
    }
    [Fact]
    public async Task LibraryUnifiesOwnedChatAndKnowledgeFilesWithoutLeakingOtherUsersOrDuplicatingOriginals()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var privateFile = await Upload(alice, "私人筆記.txt", "Private library original");
        (await alice.PostAsync($"/api/v1/files/{privateFile.Id}/retain", null)).EnsureSuccessStatusCode();
        var draft = await Upload(alice, "未送出.txt", "Still a draft");
        var collection = (await (await alice.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("政策", ""))).Content.ReadFromJsonAsync<CollectionDto>())!;
        var document = (await (await alice.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/documents", new AddDocumentRequest(privateFile.Id))).Content.ReadFromJsonAsync<DocumentDto>())!;
        var duplicate = (await (await alice.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/documents", new AddDocumentRequest(privateFile.Id))).Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(document.Id, duplicate.Id);
        var chatFile = await Upload(alice, "對話.txt", "Chat source"); var conversation = await CreateConversation(alice);
        using var run = await PostRun(alice, new(conversation.Id, "test-model", "Read file", null, null, AttachmentIds: [chatFile.Id])); run.EnsureSuccessStatusCode();
        await WaitForTerminal(alice, (await run.Content.ReadFromJsonAsync<RunDto>())!.Id);
        var library = (await alice.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!;
        Assert.Equal(2, library.Total); Assert.DoesNotContain(library.Items, x => x.File.Id == draft.Id);
        Assert.Contains(library.Items, x => x.File.Id == privateFile.Id && x.Usages.Any(u => u.Kind == "knowledge" && u.ResourceId == collection.Resource.Id));
        Assert.Contains(library.Items, x => x.File.Id == chatFile.Id && x.Usages.Any(u => u.Kind == "chat" && u.ResourceId == conversation.Id));
        Assert.All(library.Items, x => Assert.False(x.CanDelete));
        Assert.Single((await alice.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files?source=chat"))!.Items);
        Assert.Single((await alice.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files?search=私人&type=documents"))!.Items);
        Assert.Empty((await bob.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/files/{privateFile.Id}/retain", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/files/{privateFile.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.DeleteAsync($"/api/v1/files/{privateFile.Id}")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Equal(3, await db.Set<Attachment>().CountAsync());
        Assert.Single(await db.Set<KnowledgeDocument>().ToListAsync());
        var json = await alice.GetStringAsync("/api/v1/files");
        Assert.DoesNotContain("extractedText", json); Assert.DoesNotContain("\"data\"", json);
    }
    [Fact]
    public async Task ReusingACompletedReaderCopiesPagesAndRemovingIndexLeavesTheLibraryOriginal()
    {
        await using var factory = new NexusFactory(backgroundJobs: false);
        using var client = await factory.SignedInAsync();
        var file = await Upload(client, "report.txt", "An immutable source with searchable text.");
        (await client.PostAsync($"/api/v1/files/{file.Id}/retain", null)).EnsureSuccessStatusCode();
        var reader = (await (await client.PostAsync($"/api/v1/attachments/{file.Id}/document", null)).Content.ReadFromJsonAsync<DocumentDto>())!;
        using (var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services)) Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        var collection = (await (await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("來源", ""))).Content.ReadFromJsonAsync<CollectionDto>())!;
        var indexed = (await (await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/documents", new AddDocumentRequest(file.Id))).Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal((await client.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{reader.Id}/pages"))!, (await client.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{indexed.Id}/pages"))!);
        (await client.DeleteAsync($"/api/v1/documents/{indexed.Id}")).EnsureSuccessStatusCode();
        Assert.NotEmpty(await client.GetByteArrayAsync($"/api/v1/attachments/{file.Id}/content"));
        Assert.True(Assert.Single((await client.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items).CanDelete);
        (await client.DeleteAsync($"/api/v1/files/{file.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/{reader.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items);
    }
    [Fact]
    public async Task LibraryMutationsRequireCsrfAndPaginationIsBounded()
    {
        await using var factory = new NexusFactory(backgroundJobs: false); using var client = await factory.SignedInAsync();
        var file = await Upload(client, "safe.txt", "Untrusted upload");
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/files/{file.Id}/retain", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/v1/files/{file.Id}")).StatusCode);
        foreach (var query in new[] { "limit=10000", "offset=-1", "source=invalid", "type=svg" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/files?" + query)).StatusCode);
    }
}
