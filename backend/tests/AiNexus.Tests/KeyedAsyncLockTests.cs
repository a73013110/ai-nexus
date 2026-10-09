using AiNexus.Platform.Threading;
using Xunit;

namespace AiNexus.Tests;

public sealed class KeyedAsyncLockTests
{
    [Fact]
    public async Task SameKeyWaitsDifferentKeysDoNotAndUnusedKeysAreDropped()
    {
        var locks = new KeyedAsyncLock<Guid>();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var first = await locks.AcquireAsync(a, CancellationToken.None);
        var second = locks.AcquireAsync(a, CancellationToken.None);
        Assert.False(second.IsCompleted);
        // Another key is independent of the held one.
        using (await locks.AcquireAsync(b, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5))) Assert.Equal(2, locks.Count);
        Assert.Equal(1, locks.Count);
        first.Dispose();
        first.Dispose(); // A second dispose must not release the next holder's turn.
        Task<IDisposable> third;
        using (await second.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            // A waiter's task can only complete when the holder releases, so checking it at once is deterministic.
            third = locks.AcquireAsync(a, CancellationToken.None);
            Assert.False(third.IsCompleted);
            Assert.Equal(1, locks.Count);
        }
        (await third.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
        Assert.Equal(0, locks.Count);
    }

    [Fact]
    public async Task CancelledWaiterLeavesNoEntryAndDoesNotTakeTheLock()
    {
        var locks = new KeyedAsyncLock<string>(StringComparer.Ordinal);
        var held = await locks.AcquireAsync("x", CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var waiter = locks.AcquireAsync("x", cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        Assert.Equal(1, locks.Count);
        held.Dispose();
        Assert.Equal(0, locks.Count);
        using (await locks.AcquireAsync("x", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5))) Assert.Equal(1, locks.Count);
        Assert.Equal(0, locks.Count);
    }

    [Fact]
    public async Task ManyConcurrentHoldersOfOneKeyNeverOverlap()
    {
        var locks = new KeyedAsyncLock<int>();
        var inside = 0; var overlaps = 0;
        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(async () =>
        {
            using (await locks.AcquireAsync(i % 4, CancellationToken.None))
            {
                if (i % 4 == 0 && Interlocked.Increment(ref inside) > 1) Interlocked.Increment(ref overlaps);
                await Task.Yield();
                if (i % 4 == 0) Interlocked.Decrement(ref inside);
            }
        })));
        Assert.Equal(0, overlaps);
        Assert.Equal(0, locks.Count);
    }
}
