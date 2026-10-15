using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence;

public sealed class DatabaseInitializer(IOptions<DatabasesOptions> databases, ISqlDatabase<NexusMasterDatabase> master, NexusDbContext db, DatabaseSchema schema)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var nexus = databases.Value.For(NexusDbContext.DatabaseName);
        if (!nexus.Configured) throw new ExternalServiceException(Error.Unavailable("storage_not_configured"), "請先設定 Databases.Nexus 的 Server、Name，以及秘密檔的 User／Password（或 IntegratedSecurity=true）。");
        var target = nexus.Name;
        if (target != "AiNexus") throw new ExternalServiceException(Error.Invalid("database_name"), "本初始化指令只允許建立或更新 AiNexus 專用資料庫。");
        var exists = await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name = @Name", new { Name = target }, commandTimeout: 5, cancellationToken: ct);
        if (exists == 0) await master.ExecuteAsync("CREATE DATABASE [AiNexus]", commandTimeout: 30, cancellationToken: ct);
        await db.Database.MigrateAsync(ct);
        await schema.RequireCurrentAsync(ct);
    }
}
