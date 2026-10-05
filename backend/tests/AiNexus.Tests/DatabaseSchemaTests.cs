using AiNexus.BuildingBlocks;
using AiNexus.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace AiNexus.Tests;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public async Task MissingHistoryReportsEveryMigrationWithoutCreatingSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        var schema = new DatabaseSchema(db);
        Assert.Equal(db.Database.GetMigrations(), await schema.PendingMigrationsAsync(CancellationToken.None));
        var error = await Assert.ThrowsAsync<ApiException>(() => schema.RequireCurrentAsync(CancellationToken.None));
        Assert.Equal("migrations_pending", error.Code);
        Assert.Equal(503, error.Status);
        Assert.Equal(0, await TableCountAsync(db));
    }

    [Fact]
    public async Task OlderSchemaRejectsAllPendingMigrationsAndExplainsUpgrade()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        var migrations = db.Database.GetMigrations().ToArray();
        var missing = migrations.Skip(1).ToArray();
        await RecordHistoryAsync(db, migrations.Except(missing));
        var schema = new DatabaseSchema(db);
        Assert.Equal(missing, await schema.PendingMigrationsAsync(CancellationToken.None));
        var error = await Assert.ThrowsAsync<ApiException>(() => schema.RequireCurrentAsync(CancellationToken.None));
        Assert.All(missing, id => Assert.Contains(id, error.Message));
        Assert.Contains("20261005131605_FileLibraryRetention", error.Message);
        Assert.Contains("scripts/Initialize-Database.ps1", error.Message);
        Assert.Contains("db/migrations.sql", error.Message);
        Assert.Equal(migrations.Except(missing), await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(1, await TableCountAsync(db));
    }

    [Fact]
    public async Task CurrentHistoryPassesWithoutRunningMigrations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await RecordHistoryAsync(db, db.Database.GetMigrations());
        var schema = new DatabaseSchema(db);
        await schema.RequireCurrentAsync(CancellationToken.None);
        Assert.Empty(await schema.PendingMigrationsAsync(CancellationToken.None));
        Assert.Equal(1, await TableCountAsync(db));
    }

    private static NexusDbContext Context(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<NexusDbContext>().UseSqlite(connection).Options);

    private static async Task RecordHistoryAsync(NexusDbContext db, IEnumerable<string> migrations)
    {
        // Only this isolated fixture writes history; SQL Server migration bodies cannot run on SQLite.
        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        foreach (var id in migrations)
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())));
    }

    private static Task<int> TableCountAsync(NexusDbContext db)
        => db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table'").SingleAsync();
}
