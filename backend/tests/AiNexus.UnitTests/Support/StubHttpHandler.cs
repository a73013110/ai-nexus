
namespace AiNexus.UnitTests.Support;

internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(action(request));
}
