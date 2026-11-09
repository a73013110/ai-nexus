using AiNexus.Platform.Threading;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Platform;

public sealed class LeaseHeartbeatTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    /// <summary>資料庫短暫異常時租約還有效，不能因為一次續租失敗就殺掉執行中的工作。</summary>
    [Fact]
    public async Task RenewalFailureKeepsRunningAndRetries()
    {
        var clock = new FakeTimeProvider();
        using var stop = new CancellationTokenSource();
        using var execution = new CancellationTokenSource();
        var calls = 0;
        var failures = 0;
        var run = LeaseHeartbeat.RunAsync(Interval, clock, _ =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new TimeoutException("database unavailable");
            return Task.FromResult(true);
        }, _ => Interlocked.Increment(ref failures), execution, stop.Token);

        await AdvanceUntilAsync(clock, () => Volatile.Read(ref calls) >= 2);

        Assert.Equal(1, Volatile.Read(ref failures));
        Assert.False(execution.IsCancellationRequested);
        await stop.CancelAsync();
        await run;
    }

    [Fact]
    public async Task LostLeaseCancelsExecutionAndStops()
    {
        var clock = new FakeTimeProvider();
        using var execution = new CancellationTokenSource();
        var run = LeaseHeartbeat.RunAsync(Interval, clock, _ => Task.FromResult(false), _ => Assert.Fail("不應視為失敗"), execution, CancellationToken.None);

        await AdvanceUntilAsync(clock, () => run.IsCompleted);

        await run;
        Assert.True(execution.IsCancellationRequested);
    }

    [Fact]
    public async Task StoppingEndsWithoutCancellingExecution()
    {
        using var stop = new CancellationTokenSource();
        using var execution = new CancellationTokenSource();
        var run = LeaseHeartbeat.RunAsync(Interval, new FakeTimeProvider(), _ => Task.FromResult(true), _ => { }, execution, stop.Token);

        await stop.CancelAsync();
        await run;

        Assert.False(execution.IsCancellationRequested);
    }

    /// <summary>工作收尾時取消續租，進行中的查詢可能丟非取消例外；不能往外丟，否則結果寫不進去。</summary>
    [Fact]
    public async Task StoppingSwallowsWhateverTheInFlightRenewalThrows()
    {
        var clock = new FakeTimeProvider();
        using var stop = new CancellationTokenSource();
        using var execution = new CancellationTokenSource();
        var renewing = new TaskCompletionSource();
        var run = LeaseHeartbeat.RunAsync(Interval, clock, async token =>
        {
            renewing.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw new InvalidOperationException("operation cancelled by user");
        }, _ => Assert.Fail("收尾時不算續租失敗"), execution, stop.Token);

        await AdvanceUntilAsync(clock, () => renewing.Task.IsCompleted);
        await stop.CancelAsync();
        await run;

        Assert.False(execution.IsCancellationRequested);
    }

    // 迴圈在另一條續體上才排下一個計時器，反覆推進直到條件成立，不必猜推進與排程的先後。
    private static async Task AdvanceUntilAsync(FakeTimeProvider clock, Func<bool> done)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!done())
        {
            timeout.Token.ThrowIfCancellationRequested();
            clock.Advance(Interval);
            await Task.Yield();
        }
    }
}
