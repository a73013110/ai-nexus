using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Diagnostics;

namespace AiNexus.Features.Admin.Monitoring.Traffic;

/// <summary>掛在 SqlClient 的共用診斷邊界上，涵蓋 EF、Dapper 與直接命令，且不讀取 SQL 文字。</summary>
public sealed class RuntimeSampler(RuntimeTraffic traffic, DependencyCatalog catalog, TimeProvider clock) : BackgroundService, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    /// <summary>程序取樣間隔；取樣會併入 <see cref="RuntimeTraffic"/> 的十秒資源區間。</summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<Guid, (string Id, long Start)> commands = new();
    private readonly List<IDisposable> subscriptions = [];
    public void OnNext(DiagnosticListener source)
    {
        if (source.Name == "SqlClientDiagnosticListener")
            lock (subscriptions) subscriptions.Add(source.Subscribe(this, name => name is "Microsoft.Data.SqlClient.WriteCommandBefore" or "Microsoft.Data.SqlClient.WriteCommandAfter" or "Microsoft.Data.SqlClient.WriteCommandError"));
    }
    // 每個 SQL 命令都會經過這裡；以 SqlClient 的強型別事件比對，不在熱路徑上以反射讀屬性。
    public void OnNext(KeyValuePair<string, object?> evt)
    {
        if (!traffic.Enabled) return;
        switch (evt.Value)
        {
            case SqlClientCommandBefore before:
                if (MonitoringSuppression.Active) return;
                var id = catalog.Sql(before.Command?.Connection);
                lock (commands)
                    if (commands.Count < 1024 && commands.TryAdd(before.OperationId, (id, Stopwatch.GetTimestamp()))) traffic.DependencyStarted(id);
                break;
            case SqlClientCommandAfter after: Finish(after.OperationId, failed: false); break;
            case SqlClientCommandError error: Finish(error.OperationId, failed: true); break;
        }
    }
    private void Finish(Guid operation, bool failed)
    {
        if (commands.TryRemove(operation, out var observed))
            traffic.DependencyFinished(observed.Id, Stopwatch.GetElapsedTime(observed.Start).TotalMilliseconds, failed);
    }
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!traffic.Enabled) return;
        using var all = DiagnosticListener.AllListeners.Subscribe(this);
        using var process = Process.GetCurrentProcess();
        var start = clock.GetUtcNow(); var previous = Stopwatch.GetTimestamp(); var cpu = process.TotalProcessorTime;
        var pause = GC.GetTotalPauseDuration(); var contentions = Monitor.LockContentionCount;
        using var timer = new PeriodicTimer(SampleInterval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                process.Refresh();
                var nextCpu = process.TotalProcessorTime; var nextPause = GC.GetTotalPauseDuration(); var nextContentions = Monitor.LockContentionCount;
                var elapsed = Stopwatch.GetElapsedTime(previous).TotalMilliseconds;
                var percent = Math.Clamp(100 * (nextCpu - cpu).TotalMilliseconds / elapsed / Environment.ProcessorCount, 0, 100);
                var paused = Math.Clamp(100 * (nextPause - pause).TotalMilliseconds / elapsed, 0, 100);
                // GC 回報的可用記憶體（已考量容器限制）；GC 尚未回報時為 0，此時以 null 表示未知。
                var limit = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                traffic.SampleResources(new(Math.Round(percent, 2), process.WorkingSet64, GC.GetTotalMemory(false), limit > 0 ? limit : null, process.Threads.Count,
                    ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount, Math.Round((nextContentions - contentions) * 1000d / elapsed, 2),
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), Math.Round(paused, 2), (clock.GetUtcNow() - start).TotalSeconds));
                cpu = nextCpu; pause = nextPause; contentions = nextContentions; previous = Stopwatch.GetTimestamp();
                // 若結束事件遺漏，這些命令會永遠佔著進行中計數；超過 10 分鐘仍未結束者視為失敗並強制收尾。
                foreach (var pair in commands.Where(x => Stopwatch.GetElapsedTime(x.Value.Start) > TimeSpan.FromMinutes(10)))
                    if (commands.TryRemove(pair.Key, out var stale)) traffic.DependencyFinished(stale.Id, 600000, true);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { lock (subscriptions) { foreach (var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear(); } }
    }
}
