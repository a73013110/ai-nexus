using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Account;
using AiNexus.Features.Attachments;
using AiNexus.Features.Inference;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using AiNexus.Features.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Attachments;

public sealed class FileLibraryTests
{

    [Fact]
    public async Task LibraryUnifiesOwnedChatAndKnowledgeFilesWithoutLeakingOtherUsersOrDuplicatingOriginals()
    {
        await using var factory = new NexusFactory();
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
        Assert.Equal(privateFile.Size + draft.Size + chatFile.Size, library.Storage.UsedBytes);
        Assert.Equal(5_000_000_000, library.Storage.LimitBytes);
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
        await using var factory = new NexusFactory();
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
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        var file = await Upload(client, "safe.txt", "Untrusted upload");
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/files/{file.Id}/retain", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/v1/files/{file.Id}")).StatusCode);
        foreach (var query in new[] { "limit=10000", "offset=-1", "source=invalid", "type=svg" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/files?" + query)).StatusCode);
    }

    [Fact]
    public async Task RenamePreservesOriginalBytesAndFrozenShareNamesAndRequiresExpectedName()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var file = await Upload(owner, "原始.txt", Encoding.UTF8.GetBytes("附件原始文字")); var conversation = await CreateConversation(owner);
        var runResponse = await PostRun(owner, new(conversation.Id, "test-model", "閱讀", null, null, AttachmentIds: [file.Id])); runResponse.EnsureSuccessStatusCode(); await WaitForTerminal(owner, (await runResponse.Content.ReadFromJsonAsync<RunDto>())!.Id);
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var shareResponse = await owner.PostAsJsonAsync("/api/v1/shares", new CreateShareRequest("conversation", conversation.Id, [readerId], IncludeAttachments: true)); shareResponse.EnsureSuccessStatusCode(); var share = (await shareResponse.Content.ReadFromJsonAsync<ShareDto>())!;
        var rename = await owner.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("新名稱.txt", file.FileName)); rename.EnsureSuccessStatusCode();
        Assert.Equal("新名稱.txt", (await rename.Content.ReadFromJsonAsync<AttachmentDto>())!.FileName);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("衝突.txt", file.FileName))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("執行.exe", "新名稱.txt"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.PutAsJsonAsync($"/api/v1/files/{file.Id}/name", new RenameLibraryFileRequest("偷改.txt", "新名稱.txt"))).StatusCode);
        var preview = (await reader.GetFromJsonAsync<SharedFilePreviewDto>($"/api/v1/shares/{share.Id}/files/{file.Id}/preview"))!;
        Assert.Equal(file.FileName, preview.File.FileName); Assert.Contains("附件原始文字", Assert.Single(preview.Pages).Text);
        using var inline = await reader.GetAsync($"/api/v1/shares/{share.Id}/files/{file.Id}"); inline.EnsureSuccessStatusCode(); Assert.Equal("attachment", inline.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("附件原始文字", await inline.Content.ReadAsStringAsync());
        var snapshot = (await reader.GetFromJsonAsync<SharedContentDto>($"/api/v1/shares/{share.Id}"))!;
        Assert.NotNull(snapshot.Snapshot.Messages[^1].Timing); Assert.Equal(123, snapshot.Snapshot.Messages[^1].Timing!.InputTokens);
        (await owner.DeleteAsync($"/api/v1/shares/{share.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/shares/{share.Id}/files/{file.Id}/preview")).StatusCode);
    }

    [Fact]
    public async Task DeletingConversationRetainsLibraryFileUntilOwnerExplicitlyReclaimsQuota()
    {
        await using var factory = new NexusFactory(attachments: x => { x.MaxFileBytes = 1024; x.MaxMessageBytes = 1024; x.DefaultOwnerLimitBytes = 1024; });
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var content = Encoding.UTF8.GetBytes(new string('x', 600));
        var file = await Upload(client, "first.txt", content);
        await RunWithAttachment(client, conversation.Id, file.Id);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadResponse(client, "second.txt", content)).StatusCode);
        (await client.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadResponse(client, "second.txt", content)).StatusCode);
        (await client.DeleteAsync($"/api/v1/files/{file.Id}")).EnsureSuccessStatusCode();
        await Upload(client, "second.txt", content);
    }
}
