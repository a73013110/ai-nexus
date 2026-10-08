using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

namespace AiNexus.Platform.Diagnostics;

public sealed class DiagnosticHealth : IDisposable
{
    private readonly Meter meter = new("AiNexus.Diagnostics", "1.0");
    public long QueueDepth, Accepted, Written, Replayed, WriteFailures, Lost, Sampled, Corrupt, ExportFailures;
    public long DiskBytes, PendingBytes, EstimatedSqlRows;
    private long lastFileTicks, lastSqlTicks;
    private readonly ConcurrentDictionary<string, string> failures = new(StringComparer.Ordinal);
    public DateTimeOffset? LastFileWrite { get => Timestamp(Interlocked.Read(ref lastFileTicks)); set => Interlocked.Exchange(ref lastFileTicks, value?.UtcTicks ?? 0); }
    public DateTimeOffset? LastSqlWrite { get => Timestamp(Interlocked.Read(ref lastSqlTicks)); set => Interlocked.Exchange(ref lastSqlTicks, value?.UtcTicks ?? 0); }
    public string? Failure => string.Join(",", failures.Values.Order(StringComparer.Ordinal)) is { Length: > 0 } text ? text : null;
    private static DateTimeOffset? Timestamp(long ticks) => ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
    public void Recovered(string component) => failures.TryRemove(component, out _);
    private long lastEmergency;
    private int emergencyPending;
    public DiagnosticHealth()
    {
        meter.CreateObservableGauge("nexus.logs.queue.depth", () => Interlocked.Read(ref QueueDepth));
        meter.CreateObservableCounter("nexus.logs.lost", () => Interlocked.Read(ref Lost));
        meter.CreateObservableCounter("nexus.logs.write.failures", () => Interlocked.Read(ref WriteFailures));
        meter.CreateObservableCounter("nexus.logs.sampled", () => Interlocked.Read(ref Sampled));
        meter.CreateObservableCounter("nexus.logs.replayed", () => Interlocked.Read(ref Replayed));
        meter.CreateObservableCounter("nexus.logs.export.failures", () => Interlocked.Read(ref ExportFailures));
        meter.CreateObservableGauge("nexus.logs.disk.bytes", () => Interlocked.Read(ref DiskBytes));
        meter.CreateObservableGauge("nexus.logs.pending.bytes", () => Interlocked.Read(ref PendingBytes));
        meter.CreateObservableGauge("nexus.logs.sql.rows", () => Interlocked.Read(ref EstimatedSqlRows));
    }
    public void Emergency(string code, long lost = 0, string? issueCode = null)
    {
        var component = code.StartsWith("sql_cleanup", StringComparison.Ordinal) ? "cleanup" : code.StartsWith("sql_", StringComparison.Ordinal) ? "sql" : code.StartsWith("otlp_", StringComparison.Ordinal) ? "otlp" : code.StartsWith("journal_", StringComparison.Ordinal) ? "journal" : "admission";
        failures[component] = code; Interlocked.Increment(ref WriteFailures); Interlocked.Add(ref Lost, lost);
        // Deliberately bypass ILogger, EF and all external sinks. Never include the original exception.
        var now = System.Environment.TickCount64;
        if (now - Interlocked.Read(ref lastEmergency) > 1000 && Interlocked.Exchange(ref lastEmergency, now) != now && Interlocked.CompareExchange(ref emergencyPending, 1, 0) == 0)
            ThreadPool.UnsafeQueueUserWorkItem(_ => {
                try {
                    var message = $"AiNexus diagnostics degraded: {code}; lost={Interlocked.Read(ref Lost)}; failures={Interlocked.Read(ref WriteFailures)}" + (Issues.ValidCode(issueCode) ? "; issue=" + issueCode : "");
                    try { Console.Error.WriteLine(message); } catch (IOException) { }
                    if (OperatingSystem.IsWindows())
                        try { if (System.Diagnostics.EventLog.SourceExists("AiNexus.Diagnostics")) System.Diagnostics.EventLog.WriteEntry("AiNexus.Diagnostics", message, System.Diagnostics.EventLogEntryType.Error, 9010); }
                        catch (Exception e) when (e is System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                } catch (Exception) { /* Emergency infrastructure must never terminate the application. */ }
                finally { Volatile.Write(ref emergencyPending, 0); }
            }, null);
    }
    public DiagnosticHealthDto Snapshot() => new(Failure is not null || PendingBytes > 0 || Lost > 0 || Corrupt > 0 ? "degraded" : "healthy",
        QueueDepth, Accepted, Written, Replayed, WriteFailures, Lost, Sampled, Corrupt, ExportFailures, DiskBytes, PendingBytes, EstimatedSqlRows, LastFileWrite, LastSqlWrite, Failure);
    public void Dispose() => meter.Dispose();
}
public sealed record DiagnosticHealthDto(string Status, long QueueDepth, long Accepted, long Written, long Replayed, long WriteFailures, long Lost, long Sampled, long Corrupt, long ExportFailures, long DiskBytes, long PendingBytes, long EstimatedSqlRows, DateTimeOffset? LastFileWrite, DateTimeOffset? LastSqlWrite, string? Failure);
