using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>
/// 日誌設定的稽核：這次啟動套用的設定與上一筆紀錄不同時，記一筆 <c>diagnostics.configuration.activated</c>。
/// 每個程序啟動後跑一次，資料庫暫時連不上時每 5 分鐘重試到記上為止；設定沒變就不寫。
/// 路徑與匯出端 URL 刻意排除，只留指紋，避免把內部位置寫進稽核；版本與實例證據由主機事件提供。
/// </summary>
public sealed class DiagnosticConfigurationTask(AppDbContext db, AuditTrail audit, IOptions<DiagnosticOptions> options) : IScheduledTask
{
    public static ScheduledTaskDefinition Definition { get; } = new(
        "diagnostics.record-configuration", "記錄日誌設定", "系統日誌", "啟動時套用的日誌設定和上次不同時，記一筆稽核。")
    {
        FixedSchedule = Schedule.AtStartup(TimeSpan.FromMinutes(5)),
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
        => await RecordAsync(ct) ? TaskOutcome.Done("設定有變更，已記錄") : TaskOutcome.Idle("設定沒有變更");

    /// <summary>設定的指紋與上一筆稽核不同時寫一筆；相同時不寫，回傳 false。</summary>
    public async Task<bool> RecordAsync(CancellationToken ct)
    {
        var o = options.Value;
        var snapshot = JsonSerializer.Serialize(new
        {
            serviceName = o.ServiceName, minimumLevel = o.MinimumLevel.ToString(), frameworkMinimumLevel = o.FrameworkMinimumLevel.ToString(),
            queueCapacity = o.QueueCapacity, importantQueueCapacity = o.ImportantQueueCapacity, batchSize = o.BatchSize, flushIntervalMs = o.FlushIntervalMs,
            retrySeconds = o.RetrySeconds, sqlTimeoutSeconds = o.SqlTimeoutSeconds, shutdownSeconds = o.ShutdownSeconds,
            fileSizeBytes = o.FileSizeBytes, maxDiskBytes = o.MaxDiskBytes, maxSqlRows = o.MaxSqlRows,
            fileRetentionDays = o.FileRetentionDays, retentionDays = o.RetentionDays,
            locationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(o.Directory + "\n" + o.OtlpEndpoint))),
            lowLevelSampleEvery = o.LowLevelSampleEvery, maxQueryDays = o.MaxQueryDays, maxExportRows = o.MaxExportRows, maxExportDays = o.MaxExportDays, otlpEnabled = o.OtlpEnabled,
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        var action = DiagnosticsAudit.ConfigurationActivated.Name;
        var previous = await db.AuditEvents.AsNoTracking().Where(x => x.Action == action).OrderByDescending(x => x.Id).Select(x => x.DetailsJson).FirstOrDefaultAsync(ct);
        if (PreviousFingerprint(previous) == fingerprint) return false;
        audit.Record(DiagnosticsAudit.ConfigurationActivated, Guid.Empty, details: new { fingerprint, after = JsonSerializer.Deserialize<JsonElement>(snapshot) });
        await db.SaveChangesAsync(ct);
        return true;
    }

    // 舊紀錄格式無法解析時視為沒有先前指紋：多寫一筆稽核，不讓任務失敗。
    private static string? PreviousFingerprint(string? details)
    {
        if (details is null) return null;
        try
        {
            using var document = JsonDocument.Parse(details);
            return document.RootElement.GetProperty("fingerprint").GetString();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
