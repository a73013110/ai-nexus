using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Features.Inference;
using AiNexus.Features.Repositories;
using AiNexus.Features.WebSearch;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class ConnectedProviderTests
{
    [Theory]
    [InlineData("searxng")]
    [InlineData("brave")]
    public async Task SearchUsesProviderContractAndFiltersUnsafeDuplicateSources(string provider)
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("q=%E5%85%AC%E6%96%87", request.RequestUri!.Query);
            Assert.Null(request.Headers.Authorization);
            if (provider == "brave") { Assert.Equal("api.search.brave.com", request.RequestUri.Host); Assert.Equal("fixture-search-key", Assert.Single(request.Headers.GetValues("X-Subscription-Token"))); }
            else { Assert.Equal("search.fixture", request.RequestUri.Host); Assert.Contains("format=json", request.RequestUri.Query); Assert.False(request.Headers.Contains("X-Subscription-Token")); }
            var results = "[{\"url\":\"http://192.168.2.95/private\",\"title\":\"private\",\"content\":\"no\",\"description\":\"no\"},{\"url\":\"https://example.org/source\",\"title\":\"<b>來源</b>\",\"content\":\"摘要 &amp; 內容\",\"description\":\"摘要 &amp; 內容\"},{\"url\":\"https://example.org/source\",\"content\":\"duplicate\",\"description\":\"duplicate\"}]";
            return Json(provider == "brave" ? "{\"web\":{\"results\":" + results + "}}" : "{\"results\":" + results + "}");
        }));
        var search = new WebSearchProvider(new Factory(client), Options.Create(new WebSearchOptions { Provider = provider, ApiKey = "fixture-search-key", Endpoint = "https://search.fixture/" }));
        var hit = Assert.Single(await search.SearchAsync("公文", CancellationToken.None));
        Assert.Equal("來源", hit.Title); Assert.Equal("摘要 & 內容", hit.Excerpt); Assert.Equal(1, hit.Number); Assert.Equal("https://example.org/source", hit.Url);
    }

    [Fact]
    public async Task GiteaUsesReadOnlyAuthenticatedPathsAndDoesNotExposeRemoteErrors()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("https://gitea.fixture/nested/api/v1/user", request.RequestUri!.AbsoluteUri);
            Assert.Equal("token", request.Headers.Authorization!.Scheme); Assert.Equal("fixture-token", request.Headers.Authorization.Parameter);
            return new(HttpStatusCode.Forbidden) { Content = new StringContent("PRIVATE_REMOTE_ERROR fixture-token") };
        }));
        var connector = new GiteaClient(new Factory(client), Options.Create(new GiteaOptions { BaseUrl = "https://gitea.fixture/nested/" }));
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => connector.GetAsync("fixture-token", "api/v1/user", CancellationToken.None));
        Assert.Equal(ErrorKind.Forbidden, error.Error.Kind); Assert.DoesNotContain("PRIVATE", error.Message); Assert.DoesNotContain("fixture-token", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.GetAsync("fixture-token", "https://another.test/api/v1/user", CancellationToken.None));
    }

    [Fact]
    public async Task RemoteJsonWithoutContentLengthIsStillBounded()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("{\"text\":\"" + new string('x', 20000) + "\"}"))) };
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => BoundedHttpJson.ReadAsync(response, 1000, CancellationToken.None));
        Assert.Equal("remote_response_too_large", error.Error.Code);
    }

    [Fact]
    public async Task GoogleFinalMetadataIncludesCacheAndBilledReasoningOnce()
    {
        const string body = "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"PRIVATE_THOUGHT\",\"thought\":true},{\"text\":\"回答\"}]},\"finishReason\":\"STOP\"}]}\n\ndata: {\"usageMetadata\":{\"promptTokenCount\":100,\"cachedContentTokenCount\":40,\"candidatesTokenCount\":20,\"thoughtsTokenCount\":5,\"totalTokenCount\":125}}\n\n";
        using var client = new HttpClient(new Handler(_ => { var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }; r.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream"); return r; })) { BaseAddress = new Uri("https://google.fixture/") };
        var adapter = new GoogleAiProvider(client, Options.Create(new InferenceOptions { GoogleApiKey = "fixture-google-key" }));
        var rows = new List<InferenceChunk>(); await foreach (var row in adapter.StreamAsync("fixture", [new("user", "提問")], new(8192, 512, .6, "系統"), CancellationToken.None)) rows.Add(row);
        Assert.Equal("回答", string.Concat(rows.Select(x => x.Text))); Assert.True(rows[^1].Done);
        Assert.Equal(100, rows[^1].InputTokens); Assert.Equal(40, rows[^1].CachedInputTokens); Assert.Equal(25, rows[^1].OutputTokens); Assert.Equal(5, rows[^1].ReasoningTokens);
    }

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(action(request));
    }
    private sealed class Factory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) { Assert.Equal("ControlledTools", name); return client; } }
}
