using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.RemoteFiles;

namespace AiNexus.IntegrationTests.Gd;

public sealed class GdAttachmentsTests
{
    [Fact]
    public async Task AttachmentsAreListedWithTheirGdPathAndUploader()
    {
        using var gd = FakeGd.Create();
        gd.AddUser("G001", "alice", name: "林予安");
        gd.AddAttachment("L115100200101", "L115100200102.pdf", "/attfile/115", "診斷證明書.pdf", owner: "G001", memo: "醫師證明");
        gd.AddAttachment("L115100200101", "L115100200101.jpg", "/attfile/115/", "照片.jpg");
        gd.AddAttachment("L115100200199", "L115100200199.jpg", "/attfile/115", "別張單.jpg");

        var files = await new GdAttachments(gd.Access()).ReadAsync("L115100200101", TestContext.Current.CancellationToken);

        Assert.NotNull(files);
        Assert.Equal(
            [
                new GdAttachment("照片.jpg", null, null, new RemoteFileReference("gd", "/attfile/115/L115100200101.jpg")),
                new GdAttachment("診斷證明書.pdf", "醫師證明", "林予安", new RemoteFileReference("gd", "/attfile/115/L115100200102.pdf")),
            ],
            files);
    }

    [Fact]
    public async Task RowsWhoseLocationCouldPointElsewhereAreSkipped()
    {
        using var gd = FakeGd.Create();
        foreach (var (name, folder) in new[] { ("..%2Fweb.xml", "/attfile/115"), ("a.jpg", "/WEB-INF"), ("a.jpg", "/attfile/115/../.."), ("../a.jpg", "/attfile/115"), ("a..jpg", "/attfile/115"), ("a.jpg", "http://evil.test/attfile/115") })
            gd.AddAttachment("L1", name, folder, "x");

        Assert.Empty((await new GdAttachments(gd.Access()).ReadAsync("L1", TestContext.Current.CancellationToken))!);
    }
}
