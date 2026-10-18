using System.Net;
using AiNexus.Features.Work.Repositories;
using AiNexus.Platform.Errors;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Repositories;

public sealed class GiteaClientTests
{
    private static GiteaClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new ToolsHttpClientFactory(new HttpClient(new StubHttpHandler(respond))), Options.Create(new GiteaOptions { BaseUrl = "https://gitea.fixture/nested/" }));

    [Fact]
    public async Task GiteaUsesReadOnlyAuthenticatedPathsAndDoesNotExposeRemoteErrors()
    {
        var connector = Client(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("https://gitea.fixture/nested/api/v1/user", request.RequestUri!.AbsoluteUri);
            Assert.Equal("token", request.Headers.Authorization!.Scheme); Assert.Equal("fixture-token", request.Headers.Authorization.Parameter);
            return new(HttpStatusCode.Forbidden) { Content = new StringContent("PRIVATE_REMOTE_ERROR fixture-token") };
        });
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => connector.GetAsync("fixture-token", "api/v1/user", CancellationToken.None));
        Assert.Equal("gitea_forbidden", error.Error.Code); Assert.DoesNotContain("PRIVATE", error.Message); Assert.DoesNotContain("fixture-token", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connector.GetAsync("fixture-token", "https://another.test/api/v1/user", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "gitea_unauthorized")]
    [InlineData(HttpStatusCode.NotFound, "gitea_not_found")]
    [InlineData(HttpStatusCode.BadGateway, "gitea_unavailable")]
    public async Task RemoteStatusesBecomeFixedPublicErrors(HttpStatusCode status, string code)
    {
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(_ => new(status)).GetAsync("t", "api/v1/user", CancellationToken.None));
        Assert.Equal(code, error.Error.Code);
    }

    [Fact]
    public async Task OnlyIssueCommentsCanBeWritten()
    {
        var connector = Client(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            return new(HttpStatusCode.Created) { Content = new StringContent("{\"id\":1}") };
        });
        using var posted = await connector.PostCommentAsync("t", "api/v1/repos/team/app/issues/12/comments", "初檢", CancellationToken.None);
        Assert.Equal(1, posted.RootElement.GetProperty("id").GetInt32());
        foreach (var path in new[] { "api/v1/repos/team/app/issues/12", "api/v1/repos/team/app/pulls/12/merge", "api/v1/repos/team/app/issues/12/comments/3", "api/v1/user/repos" })
            await Assert.ThrowsAsync<InvalidOperationException>(() => connector.PostCommentAsync("t", path, "x", CancellationToken.None));
    }

    [Fact]
    public async Task OversizedTextIsReportedInsteadOfBuffered()
    {
        var connector = Client(_ => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 2048)) });
        Assert.Null(await connector.GetTextAsync("t", "api/v1/repos/team/app/git/commits/a.diff", 1024, CancellationToken.None));
        Assert.Equal(2048, (await connector.GetTextAsync("t", "api/v1/repos/team/app/git/commits/a.diff", 4096, CancellationToken.None))!.Length);
    }
}
