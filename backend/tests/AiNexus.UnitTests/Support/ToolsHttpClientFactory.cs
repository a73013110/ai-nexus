
namespace AiNexus.UnitTests.Support;

internal sealed class ToolsHttpClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) { Assert.Equal("ControlledTools", name); return client; } }
