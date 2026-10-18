using AiNexus.Features.Identity.Users;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiNexus.IntegrationTests.Persistence;

[Trait("Category", "SqlServer")]
public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task CreatedDatabaseReadsLastCommittedVersionWithoutWaitingForWriters()
    {
        await SqlServerDatabase.WithDatabase(async db => {
            var sql = new DbContextSqlDatabase<NexusDbContext>(db);
            Assert.True(await sql.QuerySingleAsync<bool>("SELECT [is_read_committed_snapshot_on] FROM sys.databases WHERE [database_id] = DB_ID()"));
            var logs = new Warnings();
            Assert.True(await new DatabaseSchema(db, sql, logs).ReadCommittedSnapshotAsync(CancellationToken.None));
            Assert.Empty(logs.EventIds);

            var user = new NexusUser { Sid = "rcsi-fixture", Account = "rcsi", DisplayName = "已提交" };
            db.Add(user); await db.SaveChangesAsync();
            var connection = db.Database.GetConnectionString()!;
            await using var writer = new SqlConnection(connection); await writer.OpenAsync();
            await using var transaction = (SqlTransaction)await writer.BeginTransactionAsync();
            await writer.ExecuteAsync("UPDATE [identity].[Users] SET [DisplayName] = N'未提交' WHERE [Id] = @Id", new { user.Id }, transaction);
            // 若使用鎖定式 READ COMMITTED 會等待寫入者而逾時；列版本控制（row versioning）則回傳已提交的名稱。
            await using var reader = new SqlConnection(connection); await reader.OpenAsync();
            Assert.Equal("已提交", await reader.QuerySingleAsync<string>(new CommandDefinition("SELECT [DisplayName] FROM [identity].[Users] WHERE [Id] = @Id", new { user.Id }, commandTimeout: 5)));
            await transaction.RollbackAsync();
        });
    }

    [Fact]
    public async Task SchemaCheckWarnsWhenAnExistingDatabaseLacksReadCommittedSnapshot()
    {
        await SqlServerDatabase.WithDatabase(async db => {
            var database = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            var name = database.InitialCatalog; database.InitialCatalog = "master";
            SqlConnection.ClearAllPools();
            await using (var master = new SqlConnection(database.ConnectionString))
            {
                await master.OpenAsync();
                await master.ExecuteAsync($"ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE");
            }
            var logs = new Warnings();
            Assert.False(await new DatabaseSchema(db, new DbContextSqlDatabase<NexusDbContext>(db), logs).ReadCommittedSnapshotAsync(CancellationToken.None));
            Assert.Equal(5003, Assert.Single(logs.EventIds));
        });
    }

    private sealed class Warnings : ILogger<DatabaseSchema>
    {
        public List<int> EventIds { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) EventIds.Add(eventId.Id);
        }
    }
}
