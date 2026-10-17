using System.Runtime.CompilerServices;
using AiNexus.Features.Inference;

namespace AiNexus.IntegrationTests.Support;

public sealed class TestProvider : IInferenceProvider
{
    public bool DiscoveryFail { get; set; }
    public int DelayMs { get; set; } = 30;
    public bool Fail { get; set; }
    public bool NeverFinish { get; set; }
    /// <summary>正常結束且不產生文字，如同視覺模型遇到沒有可轉錄文字的照片時的行為。</summary>
    public bool Empty { get; set; }
    private TaskCompletionSource? hold;
    public int Calls;
    public int Concurrent;
    public int MaxConcurrent;
    private readonly List<(int Calls, TaskCompletionSource Signal)> callWaiters = [];
    public IReadOnlyList<InferenceMessage> LastMessages { get; private set; } = [];

    /// <summary>供應商被呼叫 <paramref name="calls"/> 次後即完成，讓測試不必輪詢 <see cref="Calls"/>。</summary>
    public Task WhenCalledAsync(int calls = 1)
    {
        lock (callWaiters)
        {
            if (Volatile.Read(ref Calls) >= calls) return Task.CompletedTask;
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            callWaiters.Add((calls, signal));
            return signal.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>在 <see cref="Release"/> 被呼叫前，串流會停在最後一段之前，讓測試能控制 run 保持活動的時間。</summary>
    public void Hold() => hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => hold?.TrySetResult();

    public GenerationParameters? LastParameters { get; private set; }
    public string? LastModel { get; private set; }
    public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => DiscoveryFail ? throw new HttpRequestException("fixture discovery failure") : Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model", "not-approved" });
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        var calls = Interlocked.Increment(ref Calls);
        lock (callWaiters)
            foreach (var waiter in callWaiters.Where(x => x.Calls <= calls).ToList()) { callWaiters.Remove(waiter); waiter.Signal.TrySetResult(); }
        var concurrent = Interlocked.Increment(ref Concurrent);
        MaxConcurrent = Math.Max(MaxConcurrent, concurrent);
        LastMessages = messages;
        LastParameters = parameters;
        LastModel = model;
        try
        {
            if (Fail) throw new HttpRequestException("fixture failure");
            if (Empty)
            {
                await Task.Delay(DelayMs, ct);
                yield return new InferenceChunk("", true, 123, 1, "STOP", CachedInputTokens: 0, ReasoningTokens: 0);
                yield break;
            }
            if (parameters.SystemPrompt.Contains("\"conclusion\"", StringComparison.Ordinal))
            {
                await Task.Delay(DelayMs, ct);
                yield return new InferenceChunk("{\"conclusion\":\"已完成快速初檢，未發現明確缺陷。\",\"changes\":[\"更新程式邏輯。\"],\"findings\":[],\"limitation\":\"\"}");
                yield return new InferenceChunk("", true, 123, 6, "STOP", CachedInputTokens: 0, ReasoningTokens: 0);
                yield break;
            }
            foreach (var text in new[] { "這是", "測試", "回答。" })
            {
                await Task.Delay(DelayMs, ct);
                yield return new InferenceChunk(text);
            }
            if (hold is { } gate) await gate.Task.WaitAsync(ct);
            if (NeverFinish) await Task.Delay(Timeout.Infinite, ct);
            yield return new InferenceChunk("", true, 123, 6, CachedInputTokens: 0, ReasoningTokens: 0);
        }
        finally { Interlocked.Decrement(ref Concurrent); }
    }
}
