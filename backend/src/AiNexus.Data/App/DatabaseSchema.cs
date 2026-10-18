using System.Data.Common;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Data.App;

/// <summary>主資料庫的結構與設定檢查。</summary>
public sealed partial class DatabaseSchema(
    ISqlDatabase<AppDbContext> sql,
    ILogger<DatabaseSchema> logger)
{
    /// <summary>
    /// 檢查資料庫是否已開啟 READ_COMMITTED_SNAPSHOT (RCSI)。
    /// </summary>
    /// <remarks>
    /// 當 RCSI 關閉時，讀取會被尚未提交的寫入所阻塞，造成並發讀寫互相等待。
    /// 若檢測到關閉僅記錄警告日誌而不會中斷 Host 啟動，因為既有資料庫開啟 RCSI 需要排他鎖（Exclusive Lock），
    /// 必須由 DBA 在維護期間執行，不可由應用程式啟動時自動切換。
    /// </remarks>
    /// <returns>無法連線查詢時回傳 <see langword="null"/>；其餘回傳是否啟用。</returns>
    public async Task<bool?> ReadCommittedSnapshotAsync(CancellationToken ct)
    {
        bool enabled;
        try
        {
            enabled = await sql.QuerySingleAsync<bool>(
                "SELECT [is_read_committed_snapshot_on] FROM sys.databases WHERE [database_id] = DB_ID()",
                cancellationToken: ct);
        }
        catch (DbException)
        {
            // 無法連線時由後續健康檢查報告，此處僅作為告警依據
            return null;
        }

        if (!enabled)
        {
            LogReadCommittedSnapshotOff(logger);
        }

        return enabled;
    }

    [LoggerMessage(
        EventId = 5003,
        EventName = "database.read_committed_snapshot_off",
        Level = LogLevel.Warning,
        Message = "READ_COMMITTED_SNAPSHOT is off: reads wait for uncommitted writes. Ask the DBA to enable it during maintenance.")]
    private static partial void LogReadCommittedSnapshotOff(ILogger logger);
}
