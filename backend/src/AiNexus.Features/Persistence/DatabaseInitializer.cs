using AiNexus.Platform.Errors;
using AiNexus.Platform.Data.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

public sealed class DatabaseInitializer(IConfiguration configuration, ISqlDatabase<NexusMasterDatabase> master, NexusDbContext db, DatabaseSchema schema)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Nexus"))) throw new ApiException(503, "storage_not_configured", "請先在 .local/secrets/appsettings.Secrets.json 的 Database.User／Password 設定既有 SQL 帳密。");
        var target = new SqlConnectionStringBuilder(configuration.GetConnectionString("Nexus")).InitialCatalog;
        if (target != "AiNexus") throw new ApiException(400, "database_name", "本初始化指令只允許建立或更新 AiNexus 專用資料庫。");
        var exists = await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name = @Name", new { Name = target }, commandTimeout: 5, cancellationToken: ct);
        if (exists == 0) await master.ExecuteAsync("CREATE DATABASE [AiNexus]", commandTimeout: 30, cancellationToken: ct);
        await db.Database.MigrateAsync(ct);
        await schema.RequireCurrentAsync(ct);
    }
}
