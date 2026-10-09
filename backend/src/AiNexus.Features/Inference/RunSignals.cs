namespace AiNexus.Features.Inference;

/// <summary>
/// In-process wake-ups for run event subscribers. Writers call <see cref="Notify"/> after committing new events; a
/// subscriber takes <see cref="Subscription.Next"/> before reading the database and waits on it afterwards, so a commit
/// between the read and the wait is not missed. The database stays the source of truth: subscribers still poll as a
/// fallback for writes made by another process. Entries live only while someone subscribes to the run.
/// </summary>
public sealed class RunSignals
{
    private readonly Dictionary<Guid, Entry> entries = [];

    public Subscription Subscribe(Guid run)
    {
        lock (entries)
        {
            if (!entries.TryGetValue(run, out var entry)) entries.Add(run, entry = new Entry());
            entry.Subscribers++;
            return new Subscription(this, run, entry);
        }
    }

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

    /// <summary>Runs that currently have subscribers (diagnostics and tests).</summary>
    public int Count { get { lock (entries) return entries.Count; } }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed class Entry
    {
        public TaskCompletionSource Signal = NewSignal();
        public int Subscribers;
    }

    public sealed class Subscription : IDisposable
    {
        private readonly RunSignals owner;
        private readonly Guid run;
        private readonly Entry entry;
        private int disposed;

        internal Subscription(RunSignals owner, Guid run, Entry entry) => (this.owner, this.run, this.entry) = (owner, run, entry);

        /// <summary>Completes on the first notification after this call.</summary>
        public Task Next() { lock (owner.entries) return entry.Signal.Task; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.entries)
            {
                if (--entry.Subscribers == 0) owner.entries.Remove(run);
            }
        }
    }
}
