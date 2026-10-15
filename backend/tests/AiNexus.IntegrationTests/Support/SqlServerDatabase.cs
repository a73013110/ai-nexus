using System.Data.Common;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// Real SQL Server 2025 for <c>Category=SqlServer</c> tests, from <c>AINEXUS_SQLSERVER_TEST</c>: each test gets a database
/// created the way <c>db init</c> creates AiNexus (<see cref="DatabaseInitializer.CreateAsync"/>), migrated unless
/// <c>migrate</c> is false, and dropped afterwards.
/// </summary>
internal static class SqlServerDatabase
{
    public static async Task WithDatabase(Func<NexusDbContext, Task> test, bool migrate = true)
    {
        var connection = Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未設定 AINEXUS_SQLSERVER_TEST，略過真實 SQL Server 2025 整合測試。");
        var name = "AINexus_Test_" + Guid.NewGuid().ToString("N");
        var masterConnection = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" }.ConnectionString;
        var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = name };
        await using var master = new SqlConnection(masterConnection); await master.OpenAsync();
        try
        {
            await DatabaseInitializer.CreateAsync(new MasterDatabase(masterConnection), name, CancellationToken.None);
            await using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer(settings.ConnectionString).Options);
            if (migrate) await db.Database.MigrateAsync();
            await test(db);
        }
        finally
        {
            // Also after a failed setting change: CREATE may have succeeded on its own.
            SqlConnection.ClearAllPools();
            await master.ExecuteAsync($"IF DB_ID(@Name) IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}] END", new { Name = name });
        }
    }

    /// <summary>The test server's master database, from the raw test connection string rather than <c>Databases</c> settings.</summary>
    internal sealed class MasterDatabase(string connectionString) : DapperSqlDatabase<NexusMasterDatabase>
    {
        protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return await command(connection, null);
        }
    }
}
