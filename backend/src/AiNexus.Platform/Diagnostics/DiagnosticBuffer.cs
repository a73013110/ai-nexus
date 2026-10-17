using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 記憶體內的日誌緩衝區，分為一般佇列（Normal）與重要佇列（Important：Warning 以上，或帶有 IssueCode）。
/// 支援低層級事件抽樣，佇列滿時走緊急降級流程。
/// </summary>
public sealed class DiagnosticBuffer(IOptions<DiagnosticOptions> options, DiagnosticHealth health)
{
    private readonly Channel<DiagnosticEvent> normal = Channel.CreateBounded<DiagnosticEvent>(
        new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    private readonly Channel<DiagnosticEvent> important = Channel.CreateBounded<DiagnosticEvent>(
        new BoundedChannelOptions(options.Value.ImportantQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    private long sampleSequence;

    /// <summary>
    /// 把日誌事件放進對應的緩衝佇列。非重要的低層級事件若有設定抽樣，會依比例丟棄並計入 Sampled；
    /// 佇列滿時觸發降級警報。
    /// </summary>
    public bool Enqueue(DiagnosticEvent item)
    {
        if (item.Level < LogLevel.Warning
            && item.IssueCode is null
            && options.Value.LowLevelSampleEvery > 1
            && Interlocked.Increment(ref sampleSequence) % options.Value.LowLevelSampleEvery != 0)
        {
            Interlocked.Increment(ref health.Sampled);
            return false;
        }

        var significant = item.Level >= LogLevel.Warning || item.IssueCode is not null;
        var writer = significant ? important.Writer : normal.Writer;
        Interlocked.Increment(ref health.QueueDepth);

        if (writer.TryWrite(item))
        {
            Interlocked.Increment(ref health.Accepted);
            return true;
        }

        Interlocked.Decrement(ref health.QueueDepth);
        health.Emergency(significant ? "important_queue_full" : "queue_full", 1, item.IssueCode);
        return false;
    }

    /// <summary>
    /// 從佇列批次取出已緩衝的事件。先排空重要佇列，再排一般佇列，最多取 BatchSize 筆。
    /// </summary>
    public List<DiagnosticEvent> Drain()
    {
        var result = new List<DiagnosticEvent>(options.Value.BatchSize);
        while (result.Count < options.Value.BatchSize && (important.Reader.TryRead(out var item) || normal.Reader.TryRead(out item)))
        {
            Interlocked.Decrement(ref health.QueueDepth);
            result.Add(item);
        }
        return result;
    }
}
