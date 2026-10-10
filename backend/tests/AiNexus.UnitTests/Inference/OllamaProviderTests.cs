using System.Net;
using System.Text.Json;
using AiNexus.Features.Inference;

namespace AiNexus.UnitTests.Inference;

public sealed class OllamaProviderTests
{
    [Fact]
    public async Task OllamaAdapterMapsNdjsonTextUsageAndServerParameters()
    {
        string? sent = null;
        using var client = new HttpClient(new FixtureHandler(async request =>
        {
            sent = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"message\":{\"content\":\"中文\"},\"done\":false}\n{\"message\":{\"content\":\"回答\"},\"done\":true,\"prompt_eval_count\":8,\"eval_count\":2}\n") };
        })) { BaseAddress = new Uri("http://fixture.test/") };
        var chunks = new List<InferenceChunk>();
        await foreach (var chunk in new OllamaProvider(client).StreamAsync("installed-model", [new("system", "系統"), new("user", "提問")], new(8192, 512, .6, "系統"), CancellationToken.None)) chunks.Add(chunk);
        Assert.Equal("中文回答", string.Concat(chunks.Select(x => x.Text)));
        Assert.True(chunks[^1].Done); Assert.Equal(8, chunks[^1].InputTokens); Assert.Equal(2, chunks[^1].OutputTokens);
        using var payload = JsonDocument.Parse(sent!);
        Assert.Equal(8192, payload.RootElement.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("think").GetBoolean());
    }

    [Theory]
    [InlineData("{\"message\":{\"content\":\"partial\"},\"done\":false}\n")]
    [InlineData("{\"error\":\"private provider error\"}\n")]
    public async Task TruncatedAndErroredProviderStreamsCannotPretendCompletion(string body)
    {
        using var client = new HttpClient(new FixtureHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }))) { BaseAddress = new Uri("http://fixture.test/") };
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await foreach (var chunk in new OllamaProvider(client).StreamAsync("test", [], new(8192, 512, .6, "system"), CancellationToken.None)) { } });
    }

    [Fact]
    public async Task OllamaReadsVisionMetadataAndSendsImageBytesUsingItsNativeProtocol()
    {
        using var client = new HttpClient(new FixtureHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            Assert.Equal("vision-model", json.RootElement.GetProperty("model").GetString());
            if (request.RequestUri!.AbsolutePath == "/api/show")
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"capabilities\":[\"completion\",\"vision\"]}") };
            Assert.Equal("AQID", json.RootElement.GetProperty("messages")[0].GetProperty("images")[0].GetString());
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"message\":{\"content\":\"圖片\"},\"done\":true,\"prompt_eval_count\":8,\"eval_count\":2}\n") };
        })) { BaseAddress = new Uri("http://fixture.test/") };
        var provider = new OllamaProvider(client);
        Assert.True((await provider.CapabilitiesAsync("vision-model", CancellationToken.None))!.SupportsImages);
        await foreach (var chunk in provider.StreamAsync("vision-model", [new("user", "分析圖片", [new(Guid.NewGuid(), "image/png", [1, 2, 3], 4096)])], new(8192, 512, .6, "", SupportsImages: true), CancellationToken.None)) Assert.True(chunk.Done);
    }

    [Fact]
    public async Task ModelDiscoveryUsesTheNativeInstalledCatalog()
    {
        using var client = new HttpClient(new FixtureHandler(request =>
        {
            Assert.Equal("/api/tags", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"models\":[{\"name\":\"real:8b\"}]}") });
        })) { BaseAddress = new Uri("http://fixture.test/") };
        Assert.Contains("real:8b", await new OllamaProvider(client).InstalledModelsAsync(CancellationToken.None));
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
