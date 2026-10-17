namespace AiNexus.Platform.Threading;

/// <summary>
/// 依鍵值（Key）劃分的行程內非同步互斥鎖（in-process async mutex）：
/// 持有相同鍵的操作循序執行，不同鍵之間完全並行、不互相阻塞。
/// 內部項目僅在該鍵被持有或等待時存在，無人使用時自動清除，避免鍵值字典無限膨脹。
/// 注意：本鎖不具可重新進入性（not reentrant），且協調範圍僅限於單一處理程序。
/// </summary>
public sealed class KeyedAsyncLock<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, Entry> entries;

    public KeyedAsyncLock(IEqualityComparer<TKey>? comparer = null) => entries = new(comparer);

    /// <summary>目前正被持有或等待中的鍵數量。</summary>
    public int Count { get { lock (entries) return entries.Count; } }

    public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken ct)
    {
        Entry entry;
        lock (entries)
        {
            if (!entries.TryGetValue(key, out entry!)) entries.Add(key, entry = new Entry());
            entry.Users++;
        }
        try { await entry.Gate.WaitAsync(ct).ConfigureAwait(false); }
        catch
        {
            Leave(key, entry);
            throw;
        }
        return new Releaser(this, key, entry);
    }

    private void Leave(TKey key, Entry entry)
    {
        lock (entries)
        {
            if (--entry.Users == 0) entries.Remove(key);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim without AvailableWaitHandle holds nothing to release; disposing a shared gate would throw in work still releasing it during shutdown.")]
    private sealed class Entry
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public int Users;
    }

    private sealed class Releaser(KeyedAsyncLock<TKey> owner, TKey key, Entry entry) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            // 先從字典中移除項目再釋放號誌：後續進來的呼叫者將建立全新項目，而當前等待佇列中的等待者仍持有此執行個體
            owner.Leave(key, entry);
            entry.Gate.Release();
        }
    }
}
