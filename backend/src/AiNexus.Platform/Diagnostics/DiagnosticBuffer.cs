using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

public sealed class DiagnosticBuffer(IOptions<DiagnosticOptions> options, DiagnosticHealth health)
{
    private readonly Channel<DiagnosticEvent> normal = Channel.CreateBounded<DiagnosticEvent>(new BoundedChannelOptions(options.Value.QueueCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private readonly Channel<DiagnosticEvent> important = Channel.CreateBounded<DiagnosticEvent>(new BoundedChannelOptions(options.Value.ImportantQueueCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private long sampleSequence;
    public bool Enqueue(DiagnosticEvent item)
    {
        if (item.Level < LogLevel.Warning && item.IssueCode is null && options.Value.LowLevelSampleEvery > 1 && Interlocked.Increment(ref sampleSequence) % options.Value.LowLevelSampleEvery != 0)
        { Interlocked.Increment(ref health.Sampled); return false; }
        var significant = item.Level >= LogLevel.Warning || item.IssueCode is not null;
        var writer = significant ? important.Writer : normal.Writer;
        Interlocked.Increment(ref health.QueueDepth);
        if (writer.TryWrite(item)) { Interlocked.Increment(ref health.Accepted); return true; }
        Interlocked.Decrement(ref health.QueueDepth); health.Emergency(significant ? "important_queue_full" : "queue_full", 1, item.IssueCode); return false;
    }
    public List<DiagnosticEvent> Drain()
    {
        var result = new List<DiagnosticEvent>(options.Value.BatchSize);
        while (result.Count < options.Value.BatchSize && (important.Reader.TryRead(out var item) || normal.Reader.TryRead(out item)))
        { Interlocked.Decrement(ref health.QueueDepth); result.Add(item); }
        return result;
    }
}
