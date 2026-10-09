namespace AiNexus.Platform.Concurrency;

/// <summary>
/// An in-process async mutex per key: holders of different keys never wait for each other. An entry exists only while
/// its key is held or awaited. Dispose the returned handle to release.
/// </summary>
public sealed class KeyedLock<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, Entry> entries = [];

    public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken ct)
    {
        Entry entry;
        lock (entries)
        {
            if (!entries.TryGetValue(key, out entry!)) entries[key] = entry = new();
            entry.Users++;
        }
        try { await entry.Gate.WaitAsync(ct); }
        catch
        {
            Leave(key, entry, held: false);
            throw;
        }
        return new Handle(this, key, entry);
    }

    private void Leave(TKey key, Entry entry, bool held)
    {
        lock (entries)
        {
            if (held) entry.Gate.Release();
            if (--entry.Users == 0) entries.Remove(key);
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int Users { get; set; }
    }

    private sealed class Handle(KeyedLock<TKey> owner, TKey key, Entry entry) : IDisposable
    {
        private int released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0) owner.Leave(key, entry, held: true);
        }
    }
}
