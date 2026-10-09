using System.Collections.Concurrent;
using System.Threading.Channels;
using AiNexus.Platform.Threading;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public sealed record GenerationJob(Guid RunId, Guid ConversationId, CancellationTokenSource Cancellation, string TraceId, string ParentSpanId, Guid OwnerId, string Provider);
public sealed record ProviderQueue(ChannelReader<GenerationJob> Reader, int Concurrency);

public sealed class GenerationScheduler
{
    private readonly Dictionary<string, Channel<GenerationJob>> queues = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim capacity;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> cancellations = new();
    private int count;
    private int active;
    public Guid InstanceId { get; } = Guid.NewGuid();
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    public Guid[] TrackedRuns => cancellations.Keys.ToArray();
    private readonly KeyedAsyncLock<Guid> conversations = new();
    public bool Ready { get; set; }
    public bool Generating => Volatile.Read(ref active) > 0;
    public int QueueDepth => Volatile.Read(ref count);
    public IReadOnlyList<ProviderQueue> Workers { get; }

    public GenerationScheduler(IOptions<InferenceOptions> options)
    {
        var limit = options.Value.QueueCapacity;
        capacity = new SemaphoreSlim(limit, limit);
        foreach (var provider in options.Value.ProviderConcurrency)
            queues.Add(provider.Key, Channel.CreateBounded<GenerationJob>(new BoundedChannelOptions(limit) { SingleReader = provider.Value == 1, FullMode = BoundedChannelFullMode.Wait }));
        Workers = options.Value.ProviderConcurrency.Select(x => new ProviderQueue(queues[x.Key].Reader, x.Value)).ToArray();
    }

    /// <summary>
    /// Serializes the state of one conversation: its runs (create, start, flush, cancel, finish, recovery: each reads and
    /// rewrites the run row, its event sequence and the answer message) and the conversation changes that require no
    /// active run (branch, archive, project, knowledge, duplicate, delete, share). Different conversations never wait
    /// for each other. One active run per owner and quota reservations are enforced in SQL (unique index, owner row lock).
    /// </summary>
    public Task<IDisposable> LockConversationAsync(Guid conversationId, CancellationToken ct) => conversations.AcquireAsync(conversationId, ct);
    /// <summary>Conversations currently locked or awaited (diagnostics and tests).</summary>
    public int LockedConversations => conversations.Count;

    public bool TryReserve() => capacity.Wait(0);
    public void ReleaseReservation() => capacity.Release();
    public void Enqueue(GenerationRun run, string provider)
    {
        var cancellation = new CancellationTokenSource();
        cancellations[run.Id] = cancellation;
        Interlocked.Increment(ref count);
        if (!queues[provider].Writer.TryWrite(new GenerationJob(run.Id, run.ConversationId, cancellation, run.TraceId!, run.ParentSpanId!, run.OwnerId, provider))) throw new InvalidOperationException("Reserved queue capacity was exceeded.");
    }
    public void Dequeued() => Interlocked.Decrement(ref count);
    public void Started() => Interlocked.Increment(ref active);
    public void Stopped() => Interlocked.Decrement(ref active);
    public void Cancel(Guid id) { if (cancellations.TryGetValue(id, out var token)) token.Cancel(); }
    public bool IsTracked(Guid id) => cancellations.ContainsKey(id);
    public void Finish(GenerationJob job)
    {
        cancellations.TryRemove(job.RunId, out _);
        job.Cancellation.Dispose();
        capacity.Release();
    }
}
