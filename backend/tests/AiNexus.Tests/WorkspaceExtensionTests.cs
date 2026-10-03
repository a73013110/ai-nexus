using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Library;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using Xunit;
using static AiNexus.Tests.ChatApiTests;

namespace AiNexus.Tests;

public sealed class WorkspaceExtensionTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jJRkAAAAASUVORK5CYII=");

    private static async Task<HttpResponseMessage> UploadResponse(HttpClient client, string name, byte[] bytes)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(bytes), "file", name);
        return await client.PostAsync("/api/v1/attachments", body);
    }
    private static async Task<AttachmentDto> Upload(HttpClient client, string name, byte[] bytes)
    {
        using var response = await UploadResponse(client, name, bytes);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
    }
    private static async Task<RunDto> Send(HttpClient client, Guid conversation, Guid file, Guid? regenerate = null)
    {
        using var response = await PostRun(client, new(conversation, "test-model", regenerate is null ? "請分析文件" : null, null, regenerate, AttachmentIds: regenerate is null ? [file] : null));
        response.EnsureSuccessStatusCode();
        return await WaitForTerminal(client, (await response.Content.ReadFromJsonAsync<RunDto>())!.Id);
    }

    [Fact]
    public async Task TextAttachmentReachesTheProviderAndSurvivesRegeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var file = await Upload(client, "notes.md", Encoding.UTF8.GetBytes("測試文件的重要內容"));
        var first = await Send(client, conversation.Id, file.Id);
        Assert.Contains("測試文件的重要內容", factory.Provider.LastMessages.Last().Content);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        Assert.Equal(file.Id, Assert.Single(detail.Messages.Single(x => x.Role == "user").Attachments!).Id);
        await Send(client, conversation.Id, file.Id, first.UserMessageId);
        Assert.Contains("測試文件的重要內容", factory.Provider.LastMessages.Last().Content);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
    }

    [Fact]
    public async Task ImageDataIsHydratedOnlyForGenerationAndIncludedInBudget()
    {
        await using var factory = new NexusFactory(inference: x => x.Models[0].SupportsImages = true);
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var file = await Upload(client, "image.png", Png);
        var preview = await client.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(conversation.Id, null, "describe", "test-model", [file.Id]));
        preview.EnsureSuccessStatusCode();
        Assert.True((await preview.Content.ReadFromJsonAsync<ContextUsageDto>())!.EstimatedInputTokens >= 4096);
        await Send(client, conversation.Id, file.Id);
        Assert.Equal(Png, Assert.Single(factory.Provider.LastMessages.Last().Images!).Data);
        Assert.Equal(Png, await client.GetByteArrayAsync($"/api/v1/attachments/{file.Id}/content"));
    }

    [Fact]
    public async Task TextOnlyModelRejectsImagesWithoutCreatingAUserMessage()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var file = await Upload(client, "image.png", Png);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(conversation.Id, "test-model", "describe", null, null, AttachmentIds: [file.Id]))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages);
    }

    [Fact]
    public async Task AttachmentOwnershipProtectsMetadataDownloadDeletionAndGeneration()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var file = await Upload(alice, "private.txt", Encoding.UTF8.GetBytes("private"));
        var conversation = await CreateConversation(bob);
        foreach (var suffix in new[] { "", "/content" }) Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/attachments/{file.Id}{suffix}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostRun(bob, new(conversation.Id, "test-model", "steal", null, null, AttachmentIds: [file.Id]))).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages);
    }

    [Theory]
    [InlineData("fake.png", "not an image")]
    [InlineData("evil.svg", "<svg></svg>")]
    [InlineData("invalid.pdf", "not pdf")]
    [InlineData("empty.txt", " ")]
    public async Task InvalidFilesAreRejected(string name, string content)
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadResponse(client, name, Encoding.UTF8.GetBytes(content))).StatusCode);
    }

    [Fact]
    public async Task UploadRequiresCsrfAndRejectsOversizedFiles()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadResponse(client, "large.txt", new byte[4 * 1024 * 1024 + 1])).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadResponse(client, "private.txt", Encoding.UTF8.GetBytes("private"))).StatusCode);
    }

    [Fact]
    public async Task DocumentsOverBudgetCannotCreateAnOrphanMessageOrRun()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var file = await Upload(client, "long.txt", Encoding.UTF8.GetBytes(new string('文', 4000)));
        var preview = await client.PostAsJsonAsync("/api/v1/context", new ContextPreviewRequest(null, null, "summarize", "test-model", [file.Id]));
        Assert.True((await preview.Content.ReadFromJsonAsync<ContextUsageDto>())!.BudgetExceeded);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostRun(client, new(conversation.Id, "test-model", "summarize", null, null, AttachmentIds: [file.Id]))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!.Messages);
    }

    [Fact]
    public async Task PdfAndWordContentAreExtractedBeforeInference()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        pdf.AddPage(PageSize.A4).AddText("PDF project roadmap", 12, new PdfPoint(40, 700), font);
        var pdfFile = await Upload(client, "roadmap.pdf", pdf.Build());
        await Send(client, conversation.Id, pdfFile.Id);
        Assert.Contains("PDF project roadmap", factory.Provider.LastMessages.Last().Content);
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open())) writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Word roadmap</w:t></w:r></w:p></w:body></w:document>");
        var wordFile = await Upload(client, "roadmap.docx", bytes.ToArray());
        await Send(client, conversation.Id, wordFile.Id);
        Assert.Contains("Word roadmap", factory.Provider.LastMessages.Last().Content);
    }

    [Fact]
    public async Task FavoriteArchiveLabelsAndMessageSearchRoundTrip()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client, "不同的標題");
        await WaitForTerminal(client, (await CreateRun(client, conversation.Id, "內容關鍵字 ROADMAP")).Id);
        var settings = new ConversationSettingsRequest(true, false, "回答要附上待辦事項", ["工作", "工作", "規劃"]);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", settings)).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations?view=favorites&search=ROADMAP&label=工作"))!);
        Assert.Equal(2, (await client.GetFromJsonAsync<List<string>>("/api/v1/conversations/labels"))!.Count);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", new ConversationSettingsRequest(IsArchived: true, Labels: ["工作"])) ).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations?view=archived"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await PostRun(client, new(conversation.Id, "test-model", "new", null, null))).StatusCode);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", new ConversationSettingsRequest(IsArchived: false))).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
    }

    [Fact]
    public async Task ConversationInstructionIsSnapshottedIntoGeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        (await client.PatchAsJsonAsync($"/api/v1/conversations/{conversation.Id}/settings", new ConversationSettingsRequest(SystemInstruction: "先摘要，再列待辦"))).EnsureSuccessStatusCode();
        await WaitForTerminal(client, (await CreateRun(client, conversation.Id, "hello")).Id);
        Assert.Contains("先摘要，再列待辦", factory.Provider.LastParameters!.SystemPrompt);
    }

    [Fact]
    public async Task DuplicateKeepsBranchesAndAttachmentReferencesWithNewIds()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var original = await CreateConversation(client);
        var file = await Upload(client, "notes.txt", Encoding.UTF8.GetBytes("clone attachment"));
        var run = await Send(client, original.Id, file.Id);
        await Send(client, original.Id, file.Id, run.UserMessageId);
        var copyResponse = await client.PostAsync($"/api/v1/conversations/{original.Id}/duplicate", null);
        copyResponse.EnsureSuccessStatusCode();
        var clone = (await copyResponse.Content.ReadFromJsonAsync<ConversationDto>())!;
        var cloned = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{clone.Id}"))!;
        Assert.Equal(3, cloned.Messages.Count);
        Assert.DoesNotContain(cloned.Messages, x => x.Id == run.UserMessageId);
        Assert.Equal(file.Id, Assert.Single(cloned.Messages.Single(x => x.Role == "user").Attachments!).Id);
        (await client.DeleteAsync($"/api/v1/conversations/{original.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(Encoding.UTF8.GetBytes("clone attachment"), await client.GetByteArrayAsync($"/api/v1/attachments/{file.Id}/content"));
    }

    [Fact]
    public async Task JsonBackupRecreatesTheTreeWithoutProviderIds()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var original = await CreateConversation(client);
        await WaitForTerminal(client, (await CreateRun(client, original.Id, "backup prompt")).Id);
        var backup = (await client.GetFromJsonAsync<ConversationBackup>($"/api/v1/conversations/{original.Id}/export"))!;
        var importedResponse = await client.PostAsJsonAsync("/api/v1/conversations/import", backup);
        importedResponse.EnsureSuccessStatusCode();
        var imported = (await importedResponse.Content.ReadFromJsonAsync<ConversationDto>())!;
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{imported.Id}"))!;
        Assert.Equal(2, detail.Messages.Count);
        Assert.DoesNotContain(detail.Messages, x => backup.Messages.Any(y => y.Id == x.Id));
        Assert.All(detail.Messages, x => Assert.Null(x.ModelId));
        Assert.NotNull(imported.ActiveLeafId);
    }

    [Fact]
    public async Task ImportRejectsCyclicAndCrossTreeParents()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var backup = new ConversationBackup(1, "bad", "", [], second, [new(first, second, "user", "x", "completed", DateTimeOffset.UtcNow, []), new(second, first, "assistant", "y", "completed", DateTimeOffset.UtcNow, [])]);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/conversations/import", backup)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ConversationDto>>("/api/v1/conversations"))!);
    }

    [Fact]
    public async Task PromptLibraryIsPersonalAndSupportsCrud()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var response = await alice.PostAsJsonAsync("/api/v1/prompt-templates", new SavePromptRequest("摘要", "請歸納以下內容"));
        response.EnsureSuccessStatusCode();
        var prompt = (await response.Content.ReadFromJsonAsync<PromptTemplateDto>())!;
        Assert.Empty((await bob.GetFromJsonAsync<List<PromptTemplateDto>>("/api/v1/prompt-templates"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/prompt-templates/{prompt.Id}", new SavePromptRequest("steal", "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/prompt-templates/{prompt.Id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/prompt-templates/{prompt.Id}", new SavePromptRequest("新版", "updated"))).EnsureSuccessStatusCode();
        Assert.Equal("updated", Assert.Single((await alice.GetFromJsonAsync<List<PromptTemplateDto>>("/api/v1/prompt-templates"))!).Content);
        (await alice.DeleteAsync($"/api/v1/prompt-templates/{prompt.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await alice.GetFromJsonAsync<List<PromptTemplateDto>>("/api/v1/prompt-templates"))!);
    }
}
