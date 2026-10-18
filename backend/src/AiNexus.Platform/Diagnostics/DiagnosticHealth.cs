using System.Diagnostics.Metrics;
using System.Collections.Concurrent;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 診斷管線的健康指標：追蹤佇列深度、寫入與重放次數、失敗、遺失與抽樣數量；
/// 管線降級或發生嚴重故障時，觸發緊急處置。
/// </summary>
public sealed class DiagnosticHealth : IDisposable
{
    private readonly Meter meter = new("AiNexus.Diagnostics", "1.0");

    public long QueueDepth, Accepted, Written, Replayed, WriteFailures, Lost, Sampled, Corrupt, ExportFailures;
    public long DiskBytes, PendingBytes, EstimatedSqlRows;
    private long lastFileTicks, lastSqlTicks;
    private readonly ConcurrentDictionary<string, string> failures = new(StringComparer.Ordinal);

    public DateTimeOffset? LastFileWrite
    {
        get => Timestamp(Interlocked.Read(ref lastFileTicks));
        set => Interlocked.Exchange(ref lastFileTicks, value?.UtcTicks ?? 0);
    }

    public DateTimeOffset? LastSqlWrite
    {
        get => Timestamp(Interlocked.Read(ref lastSqlTicks));
        set => Interlocked.Exchange(ref lastSqlTicks, value?.UtcTicks ?? 0);
    }

    public string? Failure => string.Join(",", failures.Values.Order(StringComparer.Ordinal)) is { Length: > 0 } text ? text : null;

    private static DateTimeOffset? Timestamp(long ticks) => ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);

    public void Recovered(string component) => failures.TryRemove(component, out _);

    private long lastEmergency;
    private int emergencyPending;

    public DiagnosticHealth()
    {
        meter.CreateObservableGauge("ainexus.logs.queue.depth", () => Interlocked.Read(ref QueueDepth));
        meter.CreateObservableCounter("ainexus.logs.lost", () => Interlocked.Read(ref Lost));
        meter.CreateObservableCounter("ainexus.logs.write.failures", () => Interlocked.Read(ref WriteFailures));
        meter.CreateObservableCounter("ainexus.logs.sampled", () => Interlocked.Read(ref Sampled));
        meter.CreateObservableCounter("ainexus.logs.replayed", () => Interlocked.Read(ref Replayed));
        meter.CreateObservableCounter("ainexus.logs.export.failures", () => Interlocked.Read(ref ExportFailures));
        meter.CreateObservableGauge("ainexus.logs.disk.bytes", () => Interlocked.Read(ref DiskBytes));
        meter.CreateObservableGauge("ainexus.logs.pending.bytes", () => Interlocked.Read(ref PendingBytes));
        meter.CreateObservableGauge("ainexus.logs.sql.rows", () => Interlocked.Read(ref EstimatedSqlRows));
    }

    /// <summary>
    /// 觸發日誌管線的緊急降級警報。刻意繞過 ILogger、EF Core 與所有外部日誌管道，也不夾帶原始例外內容。
    /// 寫入本機主控台與 Windows 事件日誌時會限流。
    /// </summary>
    public void Emergency(string code, long lost = 0, string? issueCode = null)
    {
        var component = code.StartsWith("sql_cleanup", StringComparison.Ordinal) ? "cleanup"
            : code.StartsWith("sql_", StringComparison.Ordinal) ? "sql"
            : code.StartsWith("otlp_", StringComparison.Ordinal) ? "otlp"
            : code.StartsWith("journal_", StringComparison.Ordinal) ? "journal"
            : "admission";

        failures[component] = code;
        Interlocked.Increment(ref WriteFailures);
        Interlocked.Add(ref Lost, lost);

        var now = System.Environment.TickCount64;
        if (now - Interlocked.Read(ref lastEmergency) > 1000
            && Interlocked.Exchange(ref lastEmergency, now) != now
            && Interlocked.CompareExchange(ref emergencyPending, 1, 0) == 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                try
                {
                    var message = $"AiNexus diagnostics degraded: {code}; lost={Interlocked.Read(ref Lost)}; failures={Interlocked.Read(ref WriteFailures)}"
                        + (Issues.ValidCode(issueCode) ? "; issue=" + issueCode : "");
                    try
                    {
                        Console.Error.WriteLine(message);
                    }
                    catch (IOException)
                    {
                    }

                    if (OperatingSystem.IsWindows())
                    {
                        try
                        {
                            if (System.Diagnostics.EventLog.SourceExists("AiNexus.Diagnostics"))
                            {
                                System.Diagnostics.EventLog.WriteEntry("AiNexus.Diagnostics", message, System.Diagnostics.EventLogEntryType.Error, 9010);
                            }
                        }
                        catch (Exception e) when (e is System.Security.SecurityException or InvalidOperationException or System.ComponentModel.Win32Exception)
                        {
                        }
                    }
                }
                catch (Exception)
                {
                    // 緊急回報本身不可讓整個應用程式異常終止。
                }
                finally
                {
                    Volatile.Write(ref emergencyPending, 0);
                }
            }, null);
        }
    }

    public bool Degraded => Failure is not null || Interlocked.Read(ref PendingBytes) > 0 || Interlocked.Read(ref Lost) > 0 || Interlocked.Read(ref Corrupt) > 0;

    public DiagnosticHealthSnapshot Snapshot() => new(
        Degraded,
        QueueDepth, Accepted, Written, Replayed, WriteFailures, Lost, Sampled, Corrupt, ExportFailures,
        DiskBytes, PendingBytes, EstimatedSqlRows, LastFileWrite, LastSqlWrite, Failure);

    public void Dispose() => meter.Dispose();
}

/// <summary>
/// 診斷管線目前的計量快照，供系統監控子系統讀取。
/// </summary>
public sealed record DiagnosticHealthSnapshot(
    bool Degraded,
    long QueueDepth,
    long Accepted,
    long Written,
    long Replayed,
    long WriteFailures,
    long Lost,
    long Sampled,
    long Corrupt,
    long ExportFailures,
    long DiskBytes,
    long PendingBytes,
    long EstimatedSqlRows,
    DateTimeOffset? LastFileWrite,
    DateTimeOffset? LastSqlWrite,
    string? Failure);
