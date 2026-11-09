using System.Net;
using System.Net.Http.Headers;

namespace AiNexus.IntegrationTests.Host;

public sealed class WebAssetsTests : IDisposable
{
    private const string HashedChunk = "/chunk-AbCd_-12.js";
    private readonly string webRoot = Directory.CreateTempSubdirectory("ainexus-webroot-").FullName;

    public WebAssetsTests()
    {
        File.WriteAllText(Path.Combine(webRoot, "index.html"), "<!doctype html><title>shell</title>");
        File.WriteAllText(Path.Combine(webRoot, HashedChunk.TrimStart('/')), "export const value = 1;");
        File.WriteAllText(Path.Combine(webRoot, "boot.js"), "void 0;");
    }

    public void Dispose() => Directory.Delete(webRoot, recursive: true);

    [Fact]
    public async Task HashedChunksAreCachedForever()
    {
        using var response = await GetAsync(HashedChunk);

        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
        Assert.Contains(response.Headers.CacheControl!.Extensions, x => x.Name == "immutable");
    }

    // 外殼與固定檔名的檔案指向當版的 chunk，留著舊版就載不到新功能。
    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/boot.js")]
    [InlineData("/work/chat")]
    [InlineData("/work/repositories/team/my.repo")]
    public async Task EverythingElseIsRevalidated(string path)
    {
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Null(response.Headers.CacheControl?.MaxAge);
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task StaticFilesAreCompressed(string encoding)
    {
        using var response = await GetAsync(HashedChunk, encoding);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(encoding, Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task TheShellIsCompressedOnDeepLinks()
    {
        using var response = await GetAsync("/work/chat", "br");

        Assert.Equal("br", Assert.Single(response.Content.Headers.ContentEncoding));
    }

    [Fact]
    public async Task ApiResponsesAreNotCompressed()
    {
        using var response = await GetAsync("/api/v1/me", "br");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? encoding = null)
    {
        await using var factory = new AppFactory(webRoot: webRoot);
        using var client = await factory.SignedInAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (encoding is not null) request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
        return await client.SendAsync(request);
    }
}
