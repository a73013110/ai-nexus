namespace AiNexus.Platform.Threading;

/// <summary>
/// 依鍵（Key）區分的程序內非同步互斥鎖：同一個鍵的操作依序執行，不同鍵之間完全並行、互不阻塞。
/// 項目只在鍵被持有或等待時存在，沒有人使用就會清除，避免字典無限成長。
/// 注意：本鎖不可重入（not reentrant），協調範圍也只限於單一程序。
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
            // 先從字典移除再釋放號誌：之後進來的呼叫者會建立新項目；已在佇列中的等待者仍持有這個舊執行個體，會依序取得號誌。
            owner.Leave(key, entry);
            entry.Gate.Release();
        }
    }
}
