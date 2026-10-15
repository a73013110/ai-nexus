using System.Data.Common;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

public sealed partial class DatabaseSchema(NexusDbContext db, ISqlDatabase<NexusDbContext> sql, ILogger<DatabaseSchema> logger)
{
    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken ct)
        => (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();

    public async Task RequireCurrentAsync(CancellationToken ct)
    {
        if (db.Database.IsSqlServer() && db.Database.HasPendingModelChanges())
            throw new ExternalServiceException(Error.Unavailable("model_snapshot_mismatch"), "EF 模型與 migration snapshot 不一致，請先建立並提交對應 migration。");
        var pending = await PendingMigrationsAsync(ct);
        if (pending.Count > 0)
            throw new ExternalServiceException(Error.Unavailable("migrations_pending"), $"資料庫尚未套用 migrations：{string.Join(", ", pending)}。請停止舊 host，執行 scripts/Initialize-Database.ps1 或由 DBA 套用發布套件的 migrations.sql，再啟動此版本。");
    }

    /// <summary>
    /// Whether READ COMMITTED reads the last committed row version (READ_COMMITTED_SNAPSHOT). When it is off, a warning
    /// is logged and the host still starts: switching an existing database needs exclusive access, so it is the DBA's
    /// one-time step (docs/architecture/database.md). The connected database's row in sys.databases needs no permission.
    /// </summary>
    /// <returns><see langword="null"/> when the provider is not SQL Server or the database cannot be asked.</returns>
    public async Task<bool?> ReadCommittedSnapshotAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return null;
        bool enabled;
        try { enabled = await sql.QuerySingleAsync<bool>("SELECT [is_read_committed_snapshot_on] FROM sys.databases WHERE [database_id] = DB_ID()", cancellationToken: ct); }
        // Only a warning depends on it; an unreachable database is reported by the schema check and the health check.
        catch (DbException) { return null; }
        if (!enabled) LogReadCommittedSnapshotOff(logger);
        return enabled;
    }

    [LoggerMessage(EventId = 5003, EventName = "database.read_committed_snapshot_off", Level = LogLevel.Warning, Message = "READ_COMMITTED_SNAPSHOT is off: reads wait for uncommitted writes. Ask the DBA to enable it during maintenance.")]
    private static partial void LogReadCommittedSnapshotOff(ILogger logger);
}
