using System.Net;
using System.Net.Http.Json;
using AiNexus.Modules.Artifacts;
using AiNexus.Modules.Collaboration;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class ArtifactTests
{
    private const string Content = "## 工作重點\n\n**保留事實**與必要資訊。\n\n- 第一項\n- 第二項\n\n| 項目 | 說明 |\n| --- | --- |\n| 文件 | 來源核對 |\n\n```csharp\nvar answer = 42;\n```";
    private static async Task<ArtifactDto> Create(HttpClient client, Guid? source = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("工作成果", Content, source)); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ArtifactDto>())!;
    }
    [Fact]
    public async Task ImmutableVersionsConflictRatherThanOverwritingAndReadonlyMembersCannotEdit()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var artifact = await Create(alice); var id = artifact.Resource.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/artifacts/{id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("修改後標題", "新版本內容", 1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("晚到編輯", "不應覆蓋", 1))).StatusCode);
        var original = (await alice.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{id}?version=1"))!; Assert.Equal(Content, original.Content); Assert.Equal(2, original.CurrentVersion); Assert.Equal("工作成果", original.Resource.Name);
        var user = (await bob.GetFromJsonAsync<AiNexus.BuildingBlocks.MeDto>("/api/v1/me"))!;
        (await alice.PutAsJsonAsync($"/api/v1/artifacts/{id}/access", new ResourceAclRequest([new(user.Id, "viewer")], []))).EnsureSuccessStatusCode();
        Assert.Equal("新版本內容", (await bob.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{id}"))!.Content);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/v1/artifacts/{id}", new SaveArtifactRequest("讀者編輯", "應拒絕", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/artifacts/{id}")).StatusCode);
        (await alice.DeleteAsync($"/api/v1/artifacts/{id}")).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/artifacts/{id}?version=1")).StatusCode);
    }
    [Fact]
    public async Task SourceMessagesRequireOwnershipAndTextTransformsUseApprovedModelAndRecordedUsage()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(alice); var run = await CreateRun(alice, conversation.Id, "Source answer"); await WaitForTerminal(alice, run.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("別人來源", Content, run.AssistantMessageId))).StatusCode);
        await Create(alice, run.AssistantMessageId);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "execute"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "rewrite", "unapproved"))).StatusCode);
        var result = await alice.PostAsJsonAsync("/api/v1/text/transform", new TransformTextRequest("原文", "summarize")); result.EnsureSuccessStatusCode(); Assert.NotEmpty((await result.Content.ReadFromJsonAsync<TransformTextDto>())!.Text);
        Assert.Contains("摘要原文", factory.Provider.LastParameters!.SystemPrompt);
    }
    [Fact]
    public async Task WordExportIsValidOpenXmlAndPreservesChineseTablesAndCode()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync(); var artifact = await Create(client);
        var response = await client.GetAsync($"/api/v1/artifacts/{artifact.Resource.Id}/export/docx"); response.EnsureSuccessStatusCode();
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()); using var word = WordprocessingDocument.Open(stream, false);
        var main = word.MainDocumentPart!.Document!;
        var errors = new OpenXmlValidator().Validate(word).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(x => x.Description + " at " + x.Path?.XPath)));
        Assert.Contains("保留事實", main.InnerText); Assert.Contains("var answer = 42;", main.InnerText); Assert.Single(main.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>());
    }
    [Fact]
    public async Task PdfExportUsesRealBrowserAndPreservesChineseWithoutExternalResources()
    {
        await using var renderer = new PdfExportRenderer(Options.Create(new ExportOptions()));
        var bytes = await renderer.RenderAsync(ArtifactExport.Html("公文成果", Content + "\n\n![remote](http://127.0.0.1:9/secret)\n\n<script>alert('unsafe')</script>"), CancellationToken.None);
        using var pdf = PdfDocument.Open(bytes); Assert.InRange(pdf.NumberOfPages, 1, 4);
        var text = string.Join("", pdf.GetPages().Select(x => x.Text)); Assert.Contains("公文成果", text); Assert.Contains("保留事實", text);
    }
}
