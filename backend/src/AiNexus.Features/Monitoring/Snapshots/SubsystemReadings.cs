using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Monitoring.Snapshots;

/// <summary>
/// 所有已註冊 <see cref="IMonitoringSource"/> 的快取讀數。
/// 呼叫端永遠不必等待：過期的來源只會啟動一次背景讀取（同時最多一個、使用獨立 scope、抑制觀測流量，
/// 逾時為 <see cref="Timeout"/>），呼叫端拿到的是上一次的讀數。沒有人要快照時完全不讀。
/// 應用程式停止後不再啟動新讀取，並最多等待進行中的讀取 <see cref="Timeout"/>，確保關機期間不再碰資料庫。
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

    /// <summary>依顯示順序回傳快取讀數；超過更新間隔的來源會在此啟動新讀取。</summary>
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

    /// <summary>等待目前已啟動的所有讀取完成；讀取本身不會拋出例外。</summary>
    public Task Settled => Task.WhenAll(Volatile.Read(ref entries)?.Select(x => x.Pending) ?? []);

    private Entry[] Entries()
    {
        if (Volatile.Read(ref entries) is { } ready) return ready;
        lock (gate)
        {
            if (entries is not null) return entries;
            // 只需要中繼資料（id、名稱、順序與更新間隔），用一個 scope 讀一次即可。
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
        // 檢視者的請求追蹤、log scope 與身分不可流入共用的背景讀取。
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
            // 監控讀取不可讓頁面失敗；該子系統會顯示為 unobserved，直到下一次讀取成功。
            if (!stopping) LogSourceUnavailable(logger, entry.Id, error.GetType().Name);
            reading = new(entry.Id, entry.Name, SubsystemStates.Unobserved, UnreadableSummary, [], clock.GetUtcNow());
        }
        entry.Complete(reading);
    }

    /// <summary>不再啟動新讀取，並短暫等待進行中的讀取；主機關閉時呼叫。</summary>
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
