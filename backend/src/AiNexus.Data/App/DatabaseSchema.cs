using System.Data.Common;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

/// <summary>
/// 資料庫 Schema 與遷移狀態檢查工具。
/// </summary>
/// <remarks>
/// 檢查三件事：EF 模型與 migration 快照是否一致（避免忘了建立 migration 就部署）、資料庫是否還有未套用的 migration（有則拒絕啟動），
/// 以及 SQL Server 的 RCSI 是否開啟（未開啟只記錄警告，不阻擋啟動）。
/// </remarks>
public sealed partial class DatabaseSchema(
    NexusDbContext db,
    ISqlDatabase<NexusDbContext> sql,
    ILogger<DatabaseSchema> logger)
{
    /// <summary>
    /// 取得資料庫中尚未套用的遷移名稱清單。
    /// </summary>
    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken ct)
        => (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();

    /// <summary>
    /// 要求資料庫 Schema 與當前程式碼模型完全一致，否則拋出 <see cref="ExternalServiceException"/> 阻止啟動。
    /// </summary>
    public async Task RequireCurrentAsync(CancellationToken ct)
    {
        if (db.Database.IsSqlServer() && db.Database.HasPendingModelChanges())
        {
            throw new ExternalServiceException(
                Error.Unavailable("model_snapshot_mismatch"),
                "EF 模型與 migration snapshot 不一致，請先建立並提交對應 migration。");
        }

        var pending = await PendingMigrationsAsync(ct);
        if (pending.Count > 0)
        {
            throw new ExternalServiceException(
                Error.Unavailable("migrations_pending"),
                $"資料庫尚未套用 migrations：{string.Join(", ", pending)}。請停止舊 host，執行 scripts/Initialize-Database.ps1 或由 DBA 套用發布套件的 migrations.sql，再啟動此版本。");
        }
    }

    /// <summary>
    /// 檢查資料庫是否已開啟 READ_COMMITTED_SNAPSHOT (RCSI)。
    /// </summary>
    /// <remarks>
    /// 當 RCSI 關閉時，讀取會被尚未提交的寫入所阻塞，造成並發讀寫互相等待。
    /// 若檢測到關閉僅記錄警告日誌而不會中斷 Host 啟動，因為既有資料庫開啟 RCSI 需要排他鎖（Exclusive Lock），
    /// 必須由 DBA 在維護期間執行，不可由應用程式啟動時自動切換。
    /// </remarks>
    /// <returns>若非 SQL Server 或無法連線查詢則回傳 <see langword="null"/>；其餘回傳是否啟用。</returns>
    public async Task<bool?> ReadCommittedSnapshotAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer())
        {
            return null;
        }

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
