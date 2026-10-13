using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Monitoring;

/// <summary>
/// Cached readings of every registered <see cref="IMonitoringSource"/>. Reading never waits: a stale source starts one
/// background read (single-flight, own scope, observer traffic suppressed, <see cref="Timeout"/>) and the last reading is
/// returned at once. Nothing is read while nobody asks for a snapshot. Once the application is stopping no read starts,
/// and stopping waits (up to <see cref="Timeout"/>) for running ones, so none touches the database while it shuts down.
/// </summary>
public sealed partial class SubsystemReadings
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    public const string UnreadableSummary = "無法讀取";
    private readonly Lock gate = new();
    private Entry[]? entries;
    private readonly IServiceScopeFactory scopes;
    private readonly IEnumerable<MonitoringSourceRegistration> registrations;
    private readonly TimeProvider clock;
    private readonly ILogger<SubsystemReadings> logger;
    private volatile bool stopping;

    public SubsystemReadings(IServiceScopeFactory scopes, IEnumerable<MonitoringSourceRegistration> registrations, TimeProvider clock,
        ILogger<SubsystemReadings> logger, IHostApplicationLifetime? lifetime = null)
    {
        this.scopes = scopes; this.registrations = registrations; this.clock = clock; this.logger = logger;
        lifetime?.ApplicationStopping.Register(Stop);
    }

    /// <summary>The cached readings, ordered for display; starts reads for sources older than their refresh interval.</summary>
    public MonitoredSubsystem[] Read()
    {
        var all = Entries();
        var now = clock.GetUtcNow();
        var result = new MonitoredSubsystem[all.Length];
        for (var i = 0; i < all.Length; i++)
        {
            var entry = all[i];
            if (!stopping && entry.Due(now) && entry.TryBegin()) entry.Pending = Start(entry);
            result[i] = entry.Current;
        }
        return result;
    }

    /// <summary>Completes when every read started so far has finished; reads never fault.</summary>
    public Task Settled => Task.WhenAll(Volatile.Read(ref entries)?.Select(x => x.Pending) ?? []);

    private Entry[] Entries()
    {
        if (Volatile.Read(ref entries) is { } ready) return ready;
        lock (gate)
        {
            if (entries is not null) return entries;
            // Metadata only: one scope to read each source's id, name, order and interval, once.
            using var scope = scopes.CreateScope();
            var built = registrations.DistinctBy(x => x.Type).Select(x => (Type: x.Type, Source: (IMonitoringSource)scope.ServiceProvider.GetRequiredService(x.Type)))
                .OrderBy(x => x.Source.Order).ThenBy(x => x.Source.Id, StringComparer.Ordinal)
                .Select(x => new Entry(x.Type, x.Source.Id, x.Source.Name, x.Source.Refresh)).ToArray();
            Volatile.Write(ref entries, built);
            return built;
        }
    }

    private Task Start(Entry entry)
    {
        // The viewer's request trace, log scopes and identity must not flow into a shared background read.
        if (ExecutionContext.IsFlowSuppressed()) return Task.Run(() => ReadAsync(entry));
        using (ExecutionContext.SuppressFlow()) return Task.Run(() => ReadAsync(entry));
    }

    private async Task ReadAsync(Entry entry)
    {
        MonitoredSubsystem reading;
        try
        {
            using var suppression = new MonitoringSuppression();
            await using var scope = scopes.CreateAsyncScope();
            var source = (IMonitoringSource)scope.ServiceProvider.GetRequiredService(entry.Type);
            using var timeout = new CancellationTokenSource(Timeout, clock);
            var result = await source.ReadAsync(timeout.Token).WaitAsync(Timeout, clock);
            reading = new(entry.Id, entry.Name, result.Status, result.Summary, result.Facts.ToArray(), clock.GetUtcNow());
        }
        catch (Exception error)
        {
            // A monitoring read must never fail the page; the subsystem shows as unobserved until the next read.
            if (!stopping) LogSourceUnavailable(logger, entry.Id, error.GetType().Name);
            reading = new(entry.Id, entry.Name, SubsystemStates.Unobserved, UnreadableSummary, [], clock.GetUtcNow());
        }
        entry.Complete(reading);
    }

    /// <summary>Starts no further read and waits briefly for running ones; host shutdown calls it.</summary>
    public void Stop()
    {
        stopping = true;
        Settled.Wait(Timeout);
    }

    [LoggerMessage(EventId = 3006, EventName = "monitoring.source_unavailable", Level = LogLevel.Warning, Message = "Monitoring source {Kind} could not be read ({ErrorType}).")]
    private static partial void LogSourceUnavailable(ILogger logger, string kind, string errorType);

    private sealed class Entry(Type type, string id, string name, TimeSpan refresh)
    {
        public Type Type { get; } = type;
        public string Id { get; } = id;
        public string Name { get; } = name;
        private MonitoredSubsystem current = new(id, name, SubsystemStates.Unobserved, null, [], null);
        private int reading;
        public MonitoredSubsystem Current => Volatile.Read(ref current);
        public Task Pending { get; set; } = Task.CompletedTask;
        public bool Due(DateTimeOffset now) => Current.ObservedAt is not { } at || now - at >= refresh;
        public bool TryBegin() => Interlocked.CompareExchange(ref reading, 1, 0) == 0;
        public void Complete(MonitoredSubsystem value) { Volatile.Write(ref current, value); Volatile.Write(ref reading, 0); }
    }
}
