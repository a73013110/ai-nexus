using System.Net;
using System.Text.Json;
using AiNexus.Platform.Errors;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class GoogleAiTests
{
    private static GoogleAiProvider Provider(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://fixture.test/v1beta/") }, Options.Create(new InferenceOptions { GoogleApiKey = "fixture-only-key" }));
    [Fact]
    public async Task GoogleDiscoveryUsesAuthenticatedCatalogAndOnlyContentModels()
    {
        var provider = Provider(new Handler(request =>
        {
            Assert.Equal("fixture-only-key", request.Headers.GetValues("x-goog-api-key").Single());
            Assert.DoesNotContain("fixture-only-key", request.RequestUri!.ToString());
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"models\":[{\"name\":\"models/gemma-4-26b-a4b-it\",\"supportedGenerationMethods\":[\"generateContent\"]},{\"name\":\"models/embedding\",\"supportedGenerationMethods\":[\"embedContent\"]}]}") };
        }));
        Assert.Equal(new[] { "gemma-4-26b-a4b-it" }, await provider.InstalledModelsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GoogleStreamMapsHistoryFiltersThoughtsAndPreservesTrailingUsage()
    {
        string? payload = null;
        var provider = Provider(new Handler(request =>
        {
            Assert.EndsWith(":streamGenerateContent?alt=sse", request.RequestUri!.ToString());
            payload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"private thought\",\"thought\":true},{\"text\":\"中文\"}]}}]}\r\n\r\ndata: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"回答\"}]},\"finishReason\":\"STOP\"}]}\n\ndata: {\"usageMetadata\":{\"promptTokenCount\":12,\"candidatesTokenCount\":4}}\n\n") };
            response.Content.Headers.ContentType = new("text/event-stream"); return response;
        }));
        var chunks = new List<InferenceChunk>();
        await foreach (var chunk in provider.StreamAsync("gemma-4-26b-a4b-it", [new("system", "系統"), new("user", "問題"), new("assistant", "先前回答"), new("user", "後續")], new(8192, 2048, .6, "系統"), CancellationToken.None)) chunks.Add(chunk);
        Assert.Equal("中文回答", string.Concat(chunks.Select(x => x.Text)));
        Assert.True(chunks[^1].Done); Assert.Equal(12, chunks[^1].InputTokens); Assert.Equal(4, chunks[^1].OutputTokens);
        using var json = JsonDocument.Parse(payload!);
        Assert.Equal("model", json.RootElement.GetProperty("contents")[1].GetProperty("role").GetString());
        Assert.Equal("系統", json.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(2048, json.RootElement.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
    }

    [Theory]
    [InlineData(403, "google_key_rejected")]
    [InlineData(429, "google_quota_exceeded")]
    [InlineData(404, "google_model_unavailable")]
    public async Task GoogleErrorsAreActionableAndNeverEchoThePrivateResponse(int status, string code)
    {
        var provider = Provider(new Handler(_ => new((HttpStatusCode)status) { Content = new StringContent("private server detail with fixture-only-key") }));
        var error = await Assert.ThrowsAsync<ApiException>(() => provider.InstalledModelsAsync(CancellationToken.None));
        Assert.Equal(code, error.Code); Assert.DoesNotContain("fixture-only-key", error.Message);
    }

    [Fact]
    public async Task TruncatedGoogleStreamCannotPretendCompletion()
    {
        var provider = Provider(new Handler(_ => { var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"partial\"}]}}]}\n\n") }; response.Content.Headers.ContentType = new("text/event-stream"); return response; }));
        var error = await Assert.ThrowsAsync<ApiException>(async () => { await foreach (var chunk in provider.StreamAsync("gemma-4-26b-a4b-it", [new("user", "test")], new(8192, 512, .6, "system"), CancellationToken.None)) { } });
        Assert.Equal("provider_stream_incomplete", error.Code);
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("high")]
    public async Task GemmaThinkingUsesTheNativeGenerationConfig(string effort)
    {
        var provider = Provider(new Handler(request =>
        {
            using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal(effort, payload.RootElement.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"candidates\":[{\"finishReason\":\"STOP\"}]}\n\n") };
            response.Content.Headers.ContentType = new("text/event-stream");
            return response;
        }));
        await foreach (var chunk in provider.StreamAsync("gemma-4-26b-a4b-it", [new("user", "test")], new(8192, 512, .6, "system", effort, "google-level"), CancellationToken.None)) { }
    }

    [Fact]
    public async Task ImagePartUsesNativeInlineDataAndPreservesTheUserPrompt()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var provider = Provider(new Handler(request =>
        {
            using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            var parts = payload.RootElement.GetProperty("contents")[0].GetProperty("parts");
            Assert.Equal("describe", parts[1].GetProperty("text").GetString());
            Assert.Equal("image/png", parts[0].GetProperty("inlineData").GetProperty("mimeType").GetString());
            Assert.Equal(Convert.ToBase64String(bytes), parts[0].GetProperty("inlineData").GetProperty("data").GetString());
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data: {\"candidates\":[{\"finishReason\":\"STOP\"}]}\n\n") };
            response.Content.Headers.ContentType = new("text/event-stream"); return response;
        }));
        await foreach (var chunk in provider.StreamAsync("gemma-4-26b-a4b-it", [new("user", "describe", [new(Guid.NewGuid(), "image/png", bytes, 4096)])], new(8192, 512, .6, "system", SupportsImages: true), CancellationToken.None)) { }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(callback(request));
    }
}
