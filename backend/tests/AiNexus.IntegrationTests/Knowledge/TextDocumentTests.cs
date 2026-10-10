using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class TextDocumentTests
{
    [Fact]
    public async Task TextBodyLimitAcceptsEscapedChineseAndValidatesTheActualCharacterLimit()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync();
        var collection = (await (await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("長篇筆記", ""))).Content.ReadFromJsonAsync<CollectionDto>())!;
        var text = new string('字', 20000);
        var response = await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/text", new TextDocumentRequest("長篇文字", text)); response.EnsureSuccessStatusCode();
        var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(text, (await client.GetFromJsonAsync<TextDocumentDto>($"/api/v1/documents/{document.Id}/text"))!.Text);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/text", new TextDocumentRequest("超長文字", new string('x', 64001)))).StatusCode);
    }

    [Fact]
    public async Task EditableTextUsesCollectionAclVersionChecksAndRebuildsIndexWithANewOriginal()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var reader = await factory.SignedInAsync("bob");
        var created = await owner.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("文字來源", "")); created.EnsureSuccessStatusCode(); var collection = (await created.Content.ReadFromJsonAsync<CollectionDto>())!;
        var response = await owner.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/text", new TextDocumentRequest("文字筆記", "版本一的原始文字")); response.EnsureSuccessStatusCode(); var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(1, document.TextVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("文字筆記", "先等待索引", 1))).StatusCode);
        await Process(factory);
        var readerId = (await reader.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        (await owner.PutAsJsonAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}/access", new ResourceAclRequest([new(readerId, "viewer")], []))).EnsureSuccessStatusCode();
        Assert.Equal("版本一的原始文字", (await reader.GetFromJsonAsync<TextDocumentDto>($"/api/v1/documents/{document.Id}/text"))!.Text);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("篡改", "私人修改", 1))).StatusCode);
        var update = await owner.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("更新筆記", "版本二可檢索的內容", 1)); update.EnsureSuccessStatusCode(); Assert.Equal(2, (await update.Content.ReadFromJsonAsync<DocumentDto>())!.TextVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/v1/documents/{document.Id}/text", new TextDocumentRequest("衝突", "另一人的內容", 1))).StatusCode);
        await Process(factory);
        var pages = (await owner.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{document.Id}/pages"))!; Assert.Contains("版本二", Assert.Single(pages).Text);
        var originals = (await owner.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items;
        Assert.Equal(2, originals.Count); Assert.Contains(originals, x => x.File.FileName == "文字筆記.txt"); Assert.Contains(originals, x => x.File.FileName == "更新筆記.txt");
    }
}
