using System.Net;
using AiNexus.Features.Repositories;
using AiNexus.Platform.Errors;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Repositories;

public sealed class GiteaClientTests
{
    [Fact]
    public async Task GiteaUsesReadOnlyAuthenticatedPathsAndDoesNotExposeRemoteErrors()
    {
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("https://gitea.fixture/nested/api/v1/user", request.RequestUri!.AbsoluteUri);
            Assert.Equal("token", request.Headers.Authorization!.Scheme); Assert.Equal("fixture-token", request.Headers.Authorization.Parameter);
            return new(HttpStatusCode.Forbidden) { Content = new StringContent("PRIVATE_REMOTE_ERROR fixture-token") };
        }));
        var connector = new GiteaClient(new ToolsHttpClientFactory(client), Options.Create(new GiteaOptions { BaseUrl = "https://gitea.fixture/nested/" }));
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => connector.GetAsync("fixture-token", "api/v1/user", CancellationToken.None));
        Assert.Equal(ErrorKind.Forbidden, error.Error.Kind); Assert.DoesNotContain("PRIVATE", error.Message); Assert.DoesNotContain("fixture-token", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.GetAsync("fixture-token", "https://another.test/api/v1/user", CancellationToken.None));
    }
}
