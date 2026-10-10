namespace AiNexus.Features.Chat;

/// <summary>At most two event streams per user and 64 in total.</summary>
public sealed class SubscriptionLimits
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, int> users = [];
    private int total;

    /// <summary>A lease to dispose when the stream ends, or null when the limit is reached.</summary>
    public IDisposable? TryAcquire(Guid owner)
    {
        lock (gate)
        {
            var count = users.GetValueOrDefault(owner);
            if (count >= 2 || total >= 64) return null;
            users[owner] = count + 1;
            total++;
        }
        return new Lease(() => { lock (gate) { if (--users[owner] == 0) users.Remove(owner); total--; } });
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
