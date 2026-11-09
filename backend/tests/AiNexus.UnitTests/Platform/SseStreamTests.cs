using AiNexus.Platform.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Platform;

public sealed class SseStreamTests
{
    [Fact]
    public async Task StalledClientIsCutOffAndReportedAsSuch()
    {
        var clock = new FakeTimeProvider();
        var (http, lifetime) = Stalled();

        var sending = new SseStream(http, clock).CommentAsync("connected", CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<SseClientStalledException>(() => sending);
        Assert.True(lifetime.Aborted);
    }

    /// <summary>呼叫端自己取消時連線由框架收尾，不能被當成客戶端卡住。</summary>
    [Fact]
    public async Task CallerCancellationIsNotAStall()
    {
        var (http, lifetime) = Stalled();
        using var stop = new CancellationTokenSource();

        var sending = new SseStream(http, new FakeTimeProvider()).CommentAsync("connected", stop.Token);
        await stop.CancelAsync();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
        Assert.IsNotType<SseClientStalledException>(error);
        Assert.False(lifetime.Aborted);
    }

    private static (HttpContext Http, RecordingLifetime Lifetime) Stalled()
    {
        var lifetime = new RecordingLifetime();
        var http = new DefaultHttpContext();
        http.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        http.Response.Body = new StalledBody();
        return (http, lifetime);
    }

    private sealed class RecordingLifetime : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; }
        public bool Aborted { get; private set; }
        public void Abort() => Aborted = true;
    }

    private sealed class StalledBody : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
}
