using System.Net;
using System.Text;
using AiNexus.Features.WebSearch;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.WebSearch;

public sealed class WebSearchProviderTests
{
    [Theory]
    [InlineData("searxng")]
    [InlineData("brave")]
    public async Task SearchUsesProviderContractAndFiltersUnsafeDuplicateSources(string provider)
    {
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("q=%E5%85%AC%E6%96%87", request.RequestUri!.Query);
            Assert.Null(request.Headers.Authorization);
            if (provider == "brave") { Assert.Equal("api.search.brave.com", request.RequestUri.Host); Assert.Equal("fixture-search-key", Assert.Single(request.Headers.GetValues("X-Subscription-Token"))); }
            else { Assert.Equal("search.fixture", request.RequestUri.Host); Assert.Contains("format=json", request.RequestUri.Query); Assert.False(request.Headers.Contains("X-Subscription-Token")); }
            var results = "[{\"url\":\"http://192.168.2.95/private\",\"title\":\"private\",\"content\":\"no\",\"description\":\"no\"},{\"url\":\"https://example.org/source\",\"title\":\"<b>來源</b>\",\"content\":\"摘要 &amp; 內容\",\"description\":\"摘要 &amp; 內容\"},{\"url\":\"https://example.org/source\",\"content\":\"duplicate\",\"description\":\"duplicate\"}]";
            return Json(provider == "brave" ? "{\"web\":{\"results\":" + results + "}}" : "{\"results\":" + results + "}");
        }));
        var search = new WebSearchProvider(new ToolsHttpClientFactory(client), Options.Create(new WebSearchOptions { Provider = provider, ApiKey = "fixture-search-key", Endpoint = "https://search.fixture/" }), TimeProvider.System);
        var hit = Assert.Single(await search.SearchAsync("公文", CancellationToken.None));
        Assert.Equal("來源", hit.Title); Assert.Equal("摘要 & 內容", hit.Excerpt); Assert.Equal(1, hit.Number); Assert.Equal("https://example.org/source", hit.Url);
    }

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://127.0.0.1/private")]
    [InlineData("http://192.168.2.95/private")]
    [InlineData("https://user:pass@example.org/")]
    [InlineData("http://localhost/")]
    [InlineData("http://host.local/")]
    public void UnsafeSourceLinksAreRejected(string url) => Assert.Null(WebSearchProvider.SafeUrl(url));
}
