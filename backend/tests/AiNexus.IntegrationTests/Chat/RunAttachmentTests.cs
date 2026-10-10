using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Chat;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Chat;

public sealed class RunAttachmentTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jJRkAAAAASUVORK5CYII=");

    [Fact]
    public async Task TextAttachmentReachesTheProviderAndSurvivesRegeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await CreateConversation(client);
        var file = await Upload(client, "notes.md", Encoding.UTF8.GetBytes("測試文件的重要內容"));
        var first = await RunWithAttachment(client, conversation.Id, file.Id);
        Assert.Contains("測試文件的重要內容", factory.Provider.LastMessages[^1].Content);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        Assert.Equal(file.Id, Assert.Single(detail.Messages.Single(x => x.Role == "user").Attachments!).Id);
        await RunWithAttachment(client, conversation.Id, file.Id, first.UserMessageId);
        Assert.Contains("測試文件的重要內容", factory.Provider.LastMessages[^1].Content);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/attachments/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/v1/files/{file.Id}")).StatusCode);
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
        await RunWithAttachment(client, conversation.Id, file.Id);
        Assert.Equal(Png, Assert.Single(factory.Provider.LastMessages[^1].Images!).Data);
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
        await RunWithAttachment(client, conversation.Id, pdfFile.Id);
        Assert.Contains("PDF project roadmap", factory.Provider.LastMessages[^1].Content);
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
        {
        using (var types = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open())) types.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />");
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open())) writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Word roadmap</w:t></w:r></w:p></w:body></w:document>");
        }
        var wordFile = await Upload(client, "roadmap.docx", bytes.ToArray());
        await RunWithAttachment(client, conversation.Id, wordFile.Id);
        Assert.Contains("Word roadmap", factory.Provider.LastMessages[^1].Content);
    }
}
