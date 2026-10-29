using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Notifications;

namespace AiNexus.Features.Admin.Releases.SchemaChanges;

/// <summary>
/// 每天重算一次結構異動；出現新的異動時通知有版本頁權限的人（ADR 0028）。客戶端改了 procedure 而沒人打開監控頁，也會有人知道。
/// 「新的」以整份異動清單（物件、種類、時間）的雜湊當通知的 EventKey：清單沒變時通知去重，再改一次同一個物件時間不同就會再通知。
/// </summary>
public sealed partial class SchemaChangeWatch(
    IServiceScopeFactory scopes,
    StorageReadiness readiness,
    TimeProvider clock,
    ILogger<SchemaChangeWatch> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    // 啟動時先等一下：發版後第一次啟動要做的事很多，異動晚幾分鐘發現沒有差別。
    public static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, clock, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (readiness.Configured) await CheckAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    LogCheckFailed(logger, ex.GetType().Name);
                }
                await Task.Delay(Interval, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>算一次並視需要通知，回傳異動數。</summary>
    public async Task<int> CheckAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var changes = await scope.ServiceProvider.GetRequiredService<SchemaChangeReader>().ListAsync(ct);
        if (changes.Count == 0) return 0;

        var key = "schema-changes:" + Fingerprint(changes);
        var holders = await scope.ServiceProvider.GetRequiredService<AccessService>().HoldersAsync(FeatureCatalog.Releases, ct);
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var body = $"有 {changes.Count} 個物件與這一版應有的結構不同，可能是在正式機直接修改。請到版本頁確認，並回收到開發庫。";
        foreach (var holder in holders)
        {
            await notifications.PublishAsync(holder, key, "releases.schema_changed", "info", "資料庫結構有異動", body, "schema-changes", Guid.Empty, ct);
        }
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(ct);
        LogChangesFound(logger, changes.Count);
        return changes.Count;
    }

    private static string Fingerprint(IEnumerable<SchemaChangeDto> changes)
    {
        var lines = changes
            .Select(x => string.Join('|', x.Change, x.SchemaName, x.Name, x.ChangedAt?.UtcTicks.ToString(CultureInfo.InvariantCulture)))
            .Order(StringComparer.Ordinal);
        // EventKey 有長度上限，取雜湊的前 16 bytes 就足以分辨不同的清單。
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))), 0, 16);
    }

    [LoggerMessage(EventId = 3501, EventName = "releases.schema_changes_found", Level = LogLevel.Warning,
        Message = "{Count} database objects differ from the release snapshot.")]
    private static partial void LogChangesFound(ILogger logger, int count);

    [LoggerMessage(EventId = 3502, EventName = "releases.schema_check_failed", Level = LogLevel.Warning,
        Message = "Daily schema change check failed ({ErrorType}).")]
    private static partial void LogCheckFailed(ILogger logger, string errorType);
}
