using System.Collections.Concurrent;
using System.Threading.Channels;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Scheduling.Engine;

/// <summary>
/// 同一程序內的捷徑：手動要求叫醒排程迴圈、停止要求直接取消本機正在跑的那一輪。
/// 只是讓反應更快；跨程序一律靠資料庫（輪詢與續租時讀到的 <c>CancelRequested</c>），沒有它也一樣正確。
/// </summary>
[Singleton]
public sealed class ScheduledTaskSignal
{
    private readonly Channel<bool> wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly ConcurrentDictionary<string, CancellationTokenSource> running = new(StringComparer.Ordinal);

    public void Wake() => wake.Writer.TryWrite(true);

    /// <summary>等到被叫醒或 <paramref name="ct"/> 取消；之前累積的叫醒一次消化掉。</summary>
    public async Task WaitAsync(CancellationToken ct)
    {
        await wake.Reader.WaitToReadAsync(ct);
        while (wake.Reader.TryRead(out _)) { }
    }

    internal void Track(string key, CancellationTokenSource execution) => running[key] = execution;

    internal void Untrack(string key, CancellationTokenSource execution) => running.TryRemove(new(key, execution));

    /// <summary>這一輪若在本機執行就立即取消；在其他程序時由對方續租時讀到停止要求。</summary>
    public void CancelLocal(string key)
    {
        if (!running.TryGetValue(key, out var execution)) return;
        try
        {
            execution.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 剛好結束並釋放；沒有東西要停了。
        }
    }
}
