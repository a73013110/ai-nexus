using System.Data.Common;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Data.App;

/// <summary>主資料庫的結構與設定檢查；只讀取，不修改資料庫。</summary>
public sealed partial class DatabaseSchema(
    AppDbContext db,
    ISqlDatabase<AppDbContext> sql,
    ILogger<DatabaseSchema> logger)
{
    /// <summary>
    /// 比對 EF 模型與資料庫的實際結構（<see cref="ModelSchema"/>）。會讓程式失敗的差異記錯誤，其餘（資料庫先加的欄位、只經 SQL 存取的表）記警告。
    /// </summary>
    /// <remarks>
    /// 結構由 DBA 以 <c>database/App</c> 的 dacpac 部署，程式與資料庫可能不同版；在啟動時就擋下，比第一個請求才拋出 SQL 錯誤好查。
    /// 完整結構（索引、procedure、全文）的比對由 <c>Verify-Database.ps1</c> 負責，這裡只看 EF 讀寫的表與欄位。
    /// </remarks>
    /// <returns>全部差異，依物件名稱排序。</returns>
    public async Task<IReadOnlyList<SchemaDifference>> CompareModelAsync(CancellationToken ct)
    {
        var differences = await ModelSchema.CompareAsync(db, sql, ct);
        var blocking = differences.Where(x => x.Blocking).ToList();
        if (blocking.Count > 0)
        {
            LogModelMismatch(logger, blocking.Count, Describe(blocking));
        }
        var warnings = differences.Where(x => !x.Blocking).ToList();
        if (warnings.Count > 0)
        {
            LogModelDrift(logger, warnings.Count, Describe(warnings));
        }
        return differences;
    }

    // 日誌只保留前幾項，避免一次漂移就寫出上百行；完整清單在啟動失敗時印到主控台，或以 Verify-Database.ps1 查看。
    private static string Describe(List<SchemaDifference> differences) =>
        string.Join("; ", differences.Take(10).Select(x => x.ToString())) + (differences.Count > 10 ? "; …" : "");

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

    [LoggerMessage(
        EventId = 5004,
        EventName = "database.schema_mismatch",
        Level = LogLevel.Error,
        Message = "Database schema does not match the EF model ({Count} blocking differences); the host will not start. {Reason}")]
    private static partial void LogModelMismatch(ILogger logger, int count, string reason);

    [LoggerMessage(
        EventId = 5005,
        EventName = "database.schema_drift",
        Level = LogLevel.Warning,
        Message = "Database has {Count} differences from the EF model that do not block the host. {Reason}")]
    private static partial void LogModelDrift(ILogger logger, int count, string reason);
}
