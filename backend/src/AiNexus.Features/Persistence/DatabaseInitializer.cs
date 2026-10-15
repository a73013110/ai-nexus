using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence;

public sealed class DatabaseInitializer(IOptions<DatabasesOptions> databases, ISqlDatabase<NexusMasterDatabase> master, NexusDbContext db, DatabaseSchema schema)
{
    /// <returns>Whether READ_COMMITTED_SNAPSHOT is on; an existing database may still need the DBA's one-time switch.</returns>
    public async Task<bool> InitializeAsync(CancellationToken ct)
    {
        var nexus = databases.Value.For(NexusDbContext.DatabaseName);
        if (!nexus.Configured) throw new ExternalServiceException(Error.Unavailable("storage_not_configured"), "請先設定 Databases.Nexus 的 Server、Name，以及秘密檔的 User／Password（或 IntegratedSecurity=true）。");
        var target = nexus.Name;
        if (target != "AiNexus") throw new ExternalServiceException(Error.Invalid("database_name"), "本初始化指令只允許建立或更新 AiNexus 專用資料庫。");
        var exists = await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name = @Name", new { Name = target }, commandTimeout: 5, cancellationToken: ct);
        if (exists == 0) await CreateAsync(master, target, ct);
        await db.Database.MigrateAsync(ct);
        await schema.RequireCurrentAsync(ct);
        return await schema.ReadCommittedSnapshotAsync(ct) ?? false;
    }

    /// <summary>
    /// Creates an empty database whose READ COMMITTED reads use row versions (READ_COMMITTED_SNAPSHOT). The switch runs
    /// outside a transaction and needs the database to itself, which only a database nobody has opened yet guarantees,
    /// so an existing database is never switched here. <paramref name="name"/> is chosen by code, never by a request.
    /// </summary>
    public static async Task CreateAsync(ISqlDatabase<NexusMasterDatabase> master, string name, CancellationToken ct)
    {
        var quoted = "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
        await master.ExecuteAsync("CREATE DATABASE " + quoted, commandTimeout: 30, cancellationToken: ct);
        await master.ExecuteAsync("ALTER DATABASE " + quoted + " SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", commandTimeout: 30, cancellationToken: ct);
    }
}
