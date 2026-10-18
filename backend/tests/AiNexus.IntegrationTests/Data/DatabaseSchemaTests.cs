using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Platform.Data.Sql;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiNexus.IntegrationTests.Data;

/// <summary>啟動時對主資料庫的唯讀檢查：RCSI 與 EF 模型對實際結構的比對。</summary>
public sealed class DatabaseSchemaTests
{
    [Fact]
    public async Task CreatedDatabaseReadsLastCommittedVersionWithoutWaitingForWriters()
    {
        await TestDatabase.WithContextAsync(async db => {
            var sql = new DbContextSqlDatabase<AppDbContext>(db);
            Assert.True(await sql.QuerySingleAsync<bool>("SELECT [is_read_committed_snapshot_on] FROM sys.databases WHERE [database_id] = DB_ID()"));
            var logs = new Warnings();
            Assert.True(await new DatabaseSchema(db, sql, logs).ReadCommittedSnapshotAsync(CancellationToken.None));
            Assert.Empty(logs.EventIds);

            var user = new User { Sid = "rcsi-fixture", Account = "rcsi", DisplayName = "已提交" };
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
        await TestDatabase.WithContextAsync(async db => {
            var database = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            var name = database.InitialCatalog; database.InitialCatalog = "master";
            // 只清這個資料庫的連線池；ClearAllPools 會關掉平行測試正在用的連線。
            await using (var own = new SqlConnection(db.Database.GetConnectionString())) SqlConnection.ClearPool(own);
            await using (var master = new SqlConnection(database.ConnectionString))
            {
                await master.OpenAsync();
                await master.ExecuteAsync($"ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE");
            }
            var logs = new Warnings();
            Assert.False(await new DatabaseSchema(db, new DbContextSqlDatabase<AppDbContext>(db), logs).ReadCommittedSnapshotAsync(CancellationToken.None));
            Assert.Equal(5003, Assert.Single(logs.EventIds));
        });
    }

    [Fact]
    public async Task StartupComparisonPassesOnTheDeployedSchema()
    {
        await TestDatabase.WithContextAsync(async db => {
            var logs = new Warnings();
            var differences = await new DatabaseSchema(db, new DbContextSqlDatabase<AppDbContext>(db), logs).CompareModelAsync(CancellationToken.None);
            Assert.DoesNotContain(differences, x => x.Blocking);
            Assert.DoesNotContain(5004, logs.EventIds);
        });
    }

    [Fact]
    public async Task StartupComparisonReportsBlockingDifferencesSeparatelyFromWarnings()
    {
        await TestDatabase.WithContextAsync(async db => {
            await db.Database.GetDbConnection().ExecuteAsync("""
                ALTER TABLE [notifications].[Notifications] DROP COLUMN [Title];
                ALTER TABLE [notifications].[Notifications] ADD [Extra] int NULL;
                """);
            var logs = new Warnings();
            var differences = await new DatabaseSchema(db, new DbContextSqlDatabase<AppDbContext>(db), logs).CompareModelAsync(CancellationToken.None);
            Assert.Equal("notifications.Notifications.Title", Assert.Single(differences, x => x.Blocking).Target);
            Assert.Contains(differences, x => !x.Blocking && x.Target == "notifications.Notifications.Extra");
            Assert.Equal([5004, 5005], logs.EventIds.Order());
        });
    }

    private sealed class Warnings : ILogger<DatabaseSchema>
    {
        public List<int> EventIds { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning) EventIds.Add(eventId.Id);
        }
    }
}
