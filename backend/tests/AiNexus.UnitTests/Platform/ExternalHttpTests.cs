using System.Net;
using AiNexus.Platform.Http;

namespace AiNexus.UnitTests.Platform;

public sealed class ExternalHttpTests
{
    private const string Url = "https://example.test/";

    [Fact]
    public async Task TransientStatusIsRetriedUntilSuccess()
    {
        var handler = new Script(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var retries = new List<ExternalRetry>();

        using var response = await ExternalHttp.SendAsync(new HttpClient(handler), () => new(HttpMethod.Get, Url), 2, CancellationToken.None, retries.Add);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(new ExternalRetry(1, 503, null), Assert.Single(retries));
    }

    /// <summary>用完次數後交回最後的回應，呼叫端才能依狀態碼對應自己的錯誤。</summary>
    [Fact]
    public async Task ExhaustedRetriesReturnTheLastResponse()
    {
        var handler = new Script(HttpStatusCode.TooManyRequests);

        using var response = await ExternalHttp.SendAsync(new HttpClient(handler), () => new(HttpMethod.Get, Url), 1, CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task PermanentStatusIsNotRetried(HttpStatusCode status)
    {
        var handler = new Script(status);

        using var response = await ExternalHttp.SendAsync(new HttpClient(handler), () => new(HttpMethod.Get, Url), 2, CancellationToken.None);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ZeroRetriesLeavesTransportFailureToTheCaller()
    {
        var handler = new Script { Throws = true };

        await Assert.ThrowsAsync<HttpRequestException>(() => ExternalHttp.SendAsync(new HttpClient(handler), () => new(HttpMethod.Post, Url), 0, CancellationToken.None));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task DeadlineIsReportedAsTimedOut()
    {
        var result = await ExternalHttp.CallAsync<ExternalFailure?>(
            TimeSpan.FromMilliseconds(10),
            async token => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return null; },
            (failure, _) => failure,
            CancellationToken.None);

        Assert.Equal(ExternalFailure.TimedOut, result);
    }

    [Fact]
    public async Task CallerCancellationIsNotAFailure()
    {
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExternalHttp.CallAsync<int>(
            TimeSpan.FromMinutes(1),
            async token => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return 0; },
            (_, _) => { Assert.Fail("呼叫端取消不算外部失敗"); return 0; },
            stop.Token));
    }

    [Fact]
    public async Task TransportErrorIsReportedAsUnreachable()
    {
        var result = await ExternalHttp.CallAsync<ExternalFailure?>(
            TimeSpan.FromMinutes(1),
            _ => throw new HttpRequestException("remote body"),
            (failure, _) => failure,
            CancellationToken.None);

        Assert.Equal(ExternalFailure.Unreachable, result);
    }

    [Fact]
    public async Task OversizedBodyReadsAsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[16]) };

        Assert.Null(await BoundedHttpJson.TryReadBytesAsync(response, 8, CancellationToken.None));
    }

    private sealed class Script(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        public int Calls;
        public bool Throws;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = Calls++;
            if (Throws) throw new HttpRequestException("connection refused");
            return Task.FromResult(new HttpResponseMessage(statuses[Math.Min(call, statuses.Length - 1)]));
        }
    }
}
