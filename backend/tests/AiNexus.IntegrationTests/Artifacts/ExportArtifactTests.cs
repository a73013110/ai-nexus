using AiNexus.Features.Artifacts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using static AiNexus.IntegrationTests.Support.ArtifactApi;

namespace AiNexus.IntegrationTests.Artifacts;

public sealed class ExportArtifactTests
{
    [Fact]
    public async Task WordExportIsValidOpenXmlAndPreservesChineseTablesAndCode()
    {
        await using var factory = new NexusFactory(); using var client = await factory.SignedInAsync(); var artifact = await CreateArtifact(client);
        var response = await client.GetAsync($"/api/v1/artifacts/{artifact.Resource.Id}/export/docx"); response.EnsureSuccessStatusCode();
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()); using var word = WordprocessingDocument.Open(stream, false);
        var main = word.MainDocumentPart!.Document!;
        var errors = new OpenXmlValidator().Validate(word).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(x => x.Description + " at " + x.Path?.XPath)));
        Assert.Contains("保留事實", main.InnerText); Assert.Contains("var answer = 42;", main.InnerText); Assert.Single(main.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>());
    }

    [Fact, Trait("Category", "Browser")]
    public async Task PdfExportUsesRealBrowserAndPreservesChineseWithoutExternalResources()
    {
        TestBrowser.SkipUnlessConfigured();
        await using var renderer = new PdfExportRenderer(Options.Create(TestBrowser.ExportOptions()));
        var bytes = await renderer.RenderAsync(ArtifactExport.Html("公文成果", Content + "\n\n![remote](http://127.0.0.1:9/secret)\n\n<script>alert('unsafe')</script>"), CancellationToken.None);
        using var pdf = PdfDocument.Open(bytes); Assert.InRange(pdf.NumberOfPages, 1, 4);
        var text = string.Join("", pdf.GetPages().Select(x => x.Text)); Assert.Contains("公文成果", text); Assert.Contains("保留事實", text);
    }
}
