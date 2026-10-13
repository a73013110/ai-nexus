using System.Net;

namespace AiNexus.IntegrationTests.Host;

public sealed class SpaFallbackTests : IDisposable
{
    private readonly string webRoot = Directory.CreateTempSubdirectory("nexus-webroot-").FullName;

    public SpaFallbackTests() => File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>shell</title>");

    public void Dispose() => Directory.Delete(webRoot, recursive: true);

    [Theory]
    [InlineData("/repositories/team/nexus/compare/0a1b2c3...4d5e6f7")]
    [InlineData("/repositories/team/my.repo")]
    [InlineData("/repositories/team/my.repo/commits")]
    [InlineData("/chat")]
    public async Task DeepLinksOpenTheApplicationShellEvenWhenTheirLastSegmentHasADot(string path)
    {
        await using var factory = new NexusFactory(webRoot: webRoot);
        using var client = await factory.SignedInAsync();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>shell</title>", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // A stale chunk must fail as a missing file; answering it with the shell would hide the failure behind a MIME error.
    [Fact]
    public async Task MissingStaticFilesStayNotFound()
    {
        await using var factory = new NexusFactory(webRoot: webRoot);
        using var client = await factory.SignedInAsync();
        using var response = await client.GetAsync("/chunk-missing.js");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
