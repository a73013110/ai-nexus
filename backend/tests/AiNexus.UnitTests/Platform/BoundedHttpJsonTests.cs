using System.Net;
using System.Text;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.UnitTests.Platform;

public sealed class BoundedHttpJsonTests
{
    [Fact]
    public async Task RemoteJsonWithoutContentLengthIsStillBounded()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("{\"text\":\"" + new string('x', 20000) + "\"}"))) };
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => BoundedHttpJson.ReadAsync(response, 1000, CancellationToken.None));
        Assert.Equal("remote_response_too_large", error.Error.Code);
    }
}
