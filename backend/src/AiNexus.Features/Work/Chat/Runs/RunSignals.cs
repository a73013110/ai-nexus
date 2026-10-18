namespace AiNexus.Features.Work.Chat.Runs;

/// <summary>
/// 程序內的生成事件喚醒訊號。寫入端在資料庫 Commit 後呼叫 <see cref="Notify"/>。
/// 讀取端必須先取得 <see cref="Subscription.Next"/> 的 Task 再查詢資料庫，查完才等待。
/// 這樣寫入即使落在「查詢結束」與「開始等待」之間，已取得的 Task 仍會被完成，不會漏掉喚醒。
/// 多個程序寫入時無法跨程序喚醒，因此另有 1 秒輪詢作為後備。
/// </summary>
public sealed class RunSignals
{
    private readonly Dictionary<Guid, Entry> entries = [];

    /// <summary>訂閱指定生成的事件更新訊號。</summary>
    public Subscription Subscribe(Guid run)
    {
        lock (entries)
        {
            if (!entries.TryGetValue(run, out var entry))
            {
                entries.Add(run, entry = new Entry());
            }

            entry.Subscribers++;
            return new Subscription(this, run, entry);
        }
    }

    /// <summary>通知所有訂閱此生成的工作者有新的事件已持久化。</summary>
    public void Notify(Guid run)
    {
        TaskCompletionSource signal;
        lock (entries)
        {
            if (!entries.TryGetValue(run, out var entry)) return;
            signal = entry.Signal;
            entry.Signal = NewSignal();
        }

        signal.TrySetResult();
    }

    /// <summary>目前仍有訂閱者的生成數量（供測試與內部指標）。</summary>
    public int Count
    {
        get
        {
            lock (entries) return entries.Count;
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed class Entry
    {
        public TaskCompletionSource Signal = NewSignal();
        public int Subscribers;
    }

    /// <summary>代表針對單一生成任務的事件訂閱實體。</summary>
    public sealed class Subscription : IDisposable
    {
        private readonly RunSignals owner;
        private readonly Guid run;
        private readonly Entry entry;
        private int disposed;

        internal Subscription(RunSignals owner, Guid run, Entry entry) =>
            (this.owner, this.run, this.entry) = (owner, run, entry);

        /// <summary>取得在下一次 Notify 呼叫時會被完成的等待 Task。</summary>
        public Task Next()
        {
            lock (owner.entries) return entry.Signal.Task;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.entries)
            {
                if (--entry.Subscribers == 0)
                {
                    owner.entries.Remove(run);
                }
            }
        }
    }
}
