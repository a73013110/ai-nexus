using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public sealed record GenerationJob(Guid RunId, CancellationTokenSource Cancellation);

public sealed class GenerationScheduler
{
    private readonly Channel<GenerationJob> queue;
    private readonly SemaphoreSlim capacity;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> cancellations = new();
    private int count;
    public Guid InstanceId { get; } = Guid.NewGuid();
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    public Guid[] TrackedRuns => cancellations.Keys.ToArray();
    public SemaphoreSlim StateGate { get; } = new(1, 1);
    public bool Ready { get; set; }
    public bool Generating { get; set; }
    public int QueueDepth => Volatile.Read(ref count);
    public ChannelReader<GenerationJob> Reader => queue.Reader;

    public GenerationScheduler(IOptions<InferenceOptions> options)
    {
        var limit = options.Value.QueueCapacity;
        capacity = new SemaphoreSlim(limit, limit);
        queue = Channel.CreateBounded<GenerationJob>(new BoundedChannelOptions(limit) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    }

    public bool TryReserve() => capacity.Wait(0);
    public void ReleaseReservation() => capacity.Release();
    public void Enqueue(Guid id)
    {
        var cancellation = new CancellationTokenSource();
        cancellations[id] = cancellation;
        Interlocked.Increment(ref count);
        if (!queue.Writer.TryWrite(new GenerationJob(id, cancellation))) throw new InvalidOperationException("Reserved queue capacity was exceeded.");
    }
    public void Dequeued() => Interlocked.Decrement(ref count);
    public void Cancel(Guid id) { if (cancellations.TryGetValue(id, out var token)) token.Cancel(); }
    public bool IsTracked(Guid id) => cancellations.ContainsKey(id);
    public void Finish(GenerationJob job)
    {
        cancellations.TryRemove(job.RunId, out _);
        job.Cancellation.Dispose();
        capacity.Release();
    }
}
