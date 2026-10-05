using AiNexus.BuildingBlocks;
using EDoc.Core.Database.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Database;

public sealed class DatabaseInitializer(IConfiguration configuration, IDbHelper<INexusBootstrapDatabase> bootstrap, NexusDbContext db, DatabaseSchema schema)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Nexus"))) throw new ApiException(503, "storage_not_configured", "請先在 .local/secrets/appsettings.Secrets.json 的 Database.User／Password 設定既有 SQL 帳密。");
        var target = new SqlConnectionStringBuilder(configuration.GetConnectionString("Nexus")).InitialCatalog;
        if (target != "AiNexus") throw new ApiException(400, "database_name", "本初始化指令只允許建立或更新 AiNexus 專用資料庫。");
        var exists = await bootstrap.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name = @Name", new { Name = target }, commandTimeout: 5, cancellationToken: ct);
        if (exists == 0) await bootstrap.ExecuteAsync("CREATE DATABASE [AiNexus]", commandTimeout: 30, cancellationToken: ct);
        await schema.RequireCompatibleHistoryAsync(ct);
        await db.Database.MigrateAsync(ct);
        await schema.RequireCurrentAsync(ct);
        // Explicit initialization refreshes descriptions for indexes/constraints added by future migrations.
        using var descriptions = typeof(DatabaseInitializer).Assembly.GetManifestResourceStream("AiNexus.Database.ObjectDescriptions.sql")
            ?? throw new InvalidOperationException("Database descriptions resource is missing.");
        using var reader = new StreamReader(descriptions);
        await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct);
    }
}
