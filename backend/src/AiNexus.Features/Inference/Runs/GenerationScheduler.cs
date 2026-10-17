using System.Collections.Concurrent;
using System.Threading.Channels;
using AiNexus.Platform.Threading;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference.Runs;

/// <summary>
/// 排隊中的生成工作物件。
/// </summary>
public sealed record GenerationJob(
    Guid RunId,
    Guid ConversationId,
    CancellationTokenSource Cancellation,
    string TraceId,
    string ParentSpanId,
    Guid OwnerId,
    string Provider,
    IReadOnlyList<InferenceMessage>? Context = null);

/// <summary>
/// 供應商專屬的讀取通道與並行度設定。
/// </summary>
public sealed record ProviderQueue(ChannelReader<GenerationJob> Reader, int Concurrency);

/// <summary>
/// 生成任務排程器。
/// 統一控管全站提問生成的佇列容量、供應商分流 Channel、以及對話層級的非同步排他鎖（KeyedAsyncLock）。
/// 【架構設計】：
/// 1. 對話鎖（KeyedAsyncLock）：同一對話內的所有操作（建立、流式生成、終止、刪除、歸檔等）皆嚴格序列化，不同對話完全並行；
/// 2. 供應商通道（Bounded Channel）：每個供應商獨立維持有界隊列，由專屬 Worker 池依序處理；
/// 3. 容量保護（SemaphoreSlim）：當排隊加執行中數量達到 Capacity 上限時，新請求直接拒絕以保護伺服器記憶體。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim without AvailableWaitHandle holds nothing to release; disposing a shared gate would throw in work still releasing it during shutdown.")]
public sealed class GenerationScheduler
{
    private readonly Dictionary<string, Channel<GenerationJob>> queues = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim capacity;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> cancellations = new();
    private int count;
    private int active;

    /// <summary>伺服器實體唯一的執行識別碼。</summary>
    public Guid InstanceId { get; } = Guid.NewGuid();

    /// <summary>分散式生成租約效期（2 分鐘）。Worker 每隔一定時間續約，超時則視為崩潰由其他實例接管。</summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public Guid[] TrackedRuns => cancellations.Keys.ToArray();

    private readonly KeyedAsyncLock<Guid> conversations = new();
    private readonly TaskCompletionSource firstReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool ready;

    /// <summary>指示背景 Worker 是否已完成模型探測與租約修復並就緒。</summary>
    public bool Ready
    {
        get => ready;
        set
        {
            ready = value;
            if (value) firstReady.TrySetResult();
        }
    }

    /// <summary>等待排程器首次就緒的 Task。</summary>
    public Task WhenFirstReady => firstReady.Task;

    /// <summary>當前是否有生成任務正在執行。</summary>
    public bool Generating => Volatile.Read(ref active) > 0;

    /// <summary>當前正在執行的生成任務數量。</summary>
    public int Running => Volatile.Read(ref active);

    /// <summary>當前在 Channel 佇列中等待排隊的數量。</summary>
    public int QueueDepth => Volatile.Read(ref count);

    /// <summary>當前已佔用之全域配額（排隊中 + 執行中）。</summary>
    public int Reserved => Capacity - capacity.CurrentCount;

    /// <summary>全域佇列最大容量上限。</summary>
    public int Capacity { get; }

    /// <summary>各供應商工作者工作佇列設定清單。</summary>
    public IReadOnlyList<ProviderQueue> Workers { get; }

    public GenerationScheduler(IOptions<InferenceOptions> options)
    {
        var limit = options.Value.QueueCapacity;
        Capacity = limit;
        capacity = new SemaphoreSlim(limit, limit);

        foreach (var provider in options.Value.ProviderConcurrency)
        {
            queues.Add(provider.Key, Channel.CreateBounded<GenerationJob>(new BoundedChannelOptions(limit)
            {
                SingleReader = provider.Value == 1,
                FullMode = BoundedChannelFullMode.Wait
            }));
        }

        Workers = options.Value.ProviderConcurrency
            .Select(x => new ProviderQueue(queues[x.Key].Reader, x.Value))
            .ToArray();
    }

    /// <summary>
    /// 鎖定指定對話，確保該對話的所有狀態讀寫與操作序列化進行。
    /// 不同對話之間完全互不阻塞。
    /// </summary>
    public Task<IDisposable> LockConversationAsync(Guid conversationId, CancellationToken ct) =>
        conversations.AcquireAsync(conversationId, ct);

    /// <summary>目前正在鎖定或等待鎖定中的對話數（供監控與測試）。</summary>
    public int LockedConversations => conversations.Count;

    /// <summary>嘗試預約一個佇列容量名額，若已滿立即回傳 false。</summary>
    public bool TryReserve() => capacity.Wait(0);

    /// <summary>釋放先前預約的容量配額（若未實際入列）。</summary>
    public void ReleaseReservation() => capacity.Release();

    /// <summary>
    /// 將已預約成功的生成任務送入對應供應商的排隊 Channel。
    /// </summary>
    public void Enqueue(GenerationRun run, string provider, IReadOnlyList<InferenceMessage>? context = null)
    {
        var cancellation = new CancellationTokenSource();
        cancellations[run.Id] = cancellation;
        Interlocked.Increment(ref count);

        if (!queues[provider].Writer.TryWrite(new GenerationJob(
            run.Id,
            run.ConversationId,
            cancellation,
            run.TraceId!,
            run.ParentSpanId!,
            run.OwnerId,
            provider,
            context)))
        {
            throw new InvalidOperationException("Reserved queue capacity was exceeded.");
        }
    }

    public void Dequeued() => Interlocked.Decrement(ref count);

    public void Started() => Interlocked.Increment(ref active);

    public void Stopped() => Interlocked.Decrement(ref active);

    public void Cancel(Guid id)
    {
        if (cancellations.TryGetValue(id, out var token))
        {
            token.Cancel();
        }
    }

    /// <summary>
    /// 清理已完成任務的取消憑證與釋放全域容量計數。
    /// </summary>
    public void Finish(GenerationJob job)
    {
        cancellations.TryRemove(job.RunId, out _);
        job.Cancellation.Dispose();
        capacity.Release();
    }
}
