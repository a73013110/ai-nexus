using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Notifications;
using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Admin.Releases.SchemaChanges;

/// <summary>
/// 每天重算一次結構異動；出現新的異動時通知有版本頁權限的人（ADR 0028）。客戶端改了 procedure 而沒人打開監控頁，也會有人知道。
/// 「新的」以整份異動清單（物件、種類、時間）的雜湊當通知的 EventKey：清單沒變時通知去重，再改一次同一個物件時間不同就會再通知。
/// </summary>
public sealed partial class SchemaChangeWatchTask(
    AppDbContext db,
    SchemaChangeReader reader,
    NotificationService notifications,
    AccessService access,
    ILogger<SchemaChangeWatchTask> logger) : IScheduledTask
{
    // 結構異動是整個資料庫的狀態，沒有單一資料列可指，目標 id 一律空 GUID。
    private const string Target = "schema-changes";

    // 一次失敗就通知：這個檢查一天才跑一次，等三次就是三天沒人看著結構。
    public static ScheduledTaskDefinition Definition { get; } = new(
        "releases.schema-change-watch", "結構異動檢查", "版本", "比對資料庫結構與這一版應有的結構，出現新的異動時通知版本頁的管理者。", Schedule.Daily(8, 0))
    {
        NotifyAfterFailures = 1,
    };

    public async Task<TaskOutcome> RunAsync(ScheduledTaskContext context, CancellationToken ct)
    {
        var count = await CheckAsync(ct);
        return count == 0 ? TaskOutcome.Idle("沒有異動") : TaskOutcome.Done($"{count:N0} 個物件有異動");
    }

    /// <summary>算一次並視需要通知，回傳異動數。</summary>
    public async Task<int> CheckAsync(CancellationToken ct)
    {
        var changes = await reader.ListAsync(ct);
        if (changes.Count == 0)
        {
            // 異動已回收或還原，之前的提醒不必再等人點掉。
            await notifications.ResolveAsync(Target, Guid.Empty, ct);
            return 0;
        }

        var key = "schema-changes:" + Fingerprint(changes);
        var holders = await access.HoldersAsync(FeatureCatalog.Releases, ct);
        var body = $"有 {changes.Count} 個物件與這一版應有的結構不同，可能是在正式機直接修改。請到版本頁確認，並回收到開發庫。";
        foreach (var holder in holders)
        {
            await notifications.PublishAsync(
                new NotificationMessage(holder, key, "releases.schema_changed", "info", "資料庫結構有異動", body, Target, Guid.Empty), ct);
        }
        await db.SaveChangesAsync(ct);
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
}
