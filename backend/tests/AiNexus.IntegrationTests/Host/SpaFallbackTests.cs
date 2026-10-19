using System.Net;

namespace AiNexus.IntegrationTests.Host;

public sealed class SpaFallbackTests : IDisposable
{
    private readonly string webRoot = Directory.CreateTempSubdirectory("ainexus-webroot-").FullName;

    public SpaFallbackTests() => File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>shell</title>");

    public void Dispose() => Directory.Delete(webRoot, recursive: true);

    [Theory]
    [InlineData("/work/repositories/team/nexus/compare/0a1b2c3...4d5e6f7")]
    [InlineData("/work/repositories/team/my.repo")]
    [InlineData("/work/repositories/team/my.repo/commits")]
    [InlineData("/work/chat")]
    public async Task DeepLinksOpenTheApplicationShellEvenWhenTheirLastSegmentHasADot(string path)
    {
        await using var factory = new AppFactory(webRoot: webRoot);
        using var client = await factory.SignedInAsync();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>shell</title>", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // 過期的 chunk 必須以「找不到檔案」失敗；若改回傳 SPA 外殼，錯誤會被 MIME 類型錯誤掩蓋。
    [Fact]
    public async Task MissingStaticFilesStayNotFound()
    {
        await using var factory = new AppFactory(webRoot: webRoot);
        using var client = await factory.SignedInAsync();
        using var response = await client.GetAsync("/chunk-missing.js");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
