namespace AiNexus.Platform.Threading;

/// <summary>
/// An in-process async mutex per key: holders of the same key run one at a time, different keys never wait for each
/// other. An entry exists only while someone holds or waits for its key, so keys that are no longer used do not
/// accumulate. Not reentrant, and it coordinates one process only.
/// </summary>
public sealed class KeyedAsyncLock<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, Entry> entries;

    public KeyedAsyncLock(IEqualityComparer<TKey>? comparer = null) => entries = new(comparer);

    /// <summary>Keys currently held or awaited.</summary>
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
            // Drop the entry before releasing: a later caller then creates a fresh one, and waiters still hold this one.
            owner.Leave(key, entry);
            entry.Gate.Release();
        }
    }
}
