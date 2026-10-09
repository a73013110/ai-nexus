using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;

namespace AiNexus.Features.Monitoring;

/// <summary>SqlClient's shared diagnostic boundary covers EF, Dapper and direct commands without reading SQL text.</summary>
public sealed class RuntimeSampler(RuntimeTraffic traffic, DependencyCatalog catalog, TimeProvider clock) : BackgroundService, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ConcurrentDictionary<Guid, (string Id, long Start)> commands = new();
    private readonly List<IDisposable> subscriptions = [];
    public void OnNext(DiagnosticListener source)
    {
        if (source.Name == "SqlClientDiagnosticListener")
            lock (subscriptions) subscriptions.Add(source.Subscribe(this, name => name is "Microsoft.Data.SqlClient.WriteCommandBefore" or "Microsoft.Data.SqlClient.WriteCommandAfter" or "Microsoft.Data.SqlClient.WriteCommandError"));
    }
    public void OnNext(KeyValuePair<string, object?> evt)
    {
        if (!traffic.Enabled || evt.Value is null) return;
        var type = evt.Value.GetType();
        if (type.GetProperty("OperationId")?.GetValue(evt.Value) is not Guid operation) return;
        if (evt.Key.EndsWith("Before", StringComparison.Ordinal))
        {
            if (MonitoringSuppression.Active) return;
            var command = type.GetProperty("Command")?.GetValue(evt.Value) as DbCommand;
            var id = catalog.Sql(command?.Connection);
            lock (commands)
                if (commands.Count < 1024 && commands.TryAdd(operation, (id, Stopwatch.GetTimestamp()))) traffic.DependencyStarted(id);
        }
        else if (commands.TryRemove(operation, out var observed))
            traffic.DependencyFinished(observed.Id, Stopwatch.GetElapsedTime(observed.Start).TotalMilliseconds, evt.Key.EndsWith("Error", StringComparison.Ordinal));
    }
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!traffic.Enabled) return;
        using var all = DiagnosticListener.AllListeners.Subscribe(this);
        using var process = Process.GetCurrentProcess();
        var start = clock.GetUtcNow(); var previous = Stopwatch.GetTimestamp(); var cpu = process.TotalProcessorTime;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                process.Refresh(); var nextCpu = process.TotalProcessorTime;
                var elapsed = Stopwatch.GetElapsedTime(previous).TotalMilliseconds;
                var percent = Math.Clamp(100 * (nextCpu - cpu).TotalMilliseconds / elapsed / Environment.ProcessorCount, 0, 100);
                traffic.SampleResources(new(Math.Round(percent, 2), process.WorkingSet64, GC.GetTotalMemory(false), process.Threads.Count, (clock.GetUtcNow() - start).TotalSeconds));
                cpu = nextCpu; previous = Stopwatch.GetTimestamp();
                // Missing terminal provider events must not retain unbounded operations/in-flight counts.
                foreach (var pair in commands.Where(x => Stopwatch.GetElapsedTime(x.Value.Start) > TimeSpan.FromMinutes(10)))
                    if (commands.TryRemove(pair.Key, out var stale)) traffic.DependencyFinished(stale.Id, 600000, true);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { lock (subscriptions) { foreach (var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear(); } }
    }
}
