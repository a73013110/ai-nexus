using System.Data.Common;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data.Sql;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 供 <c>Category=SqlServer</c> 測試使用的真實 SQL Server 2025，連線字串來自 <c>AINEXUS_SQLSERVER_TEST</c>：每個測試都會取得一個資料庫，
/// 建立方式與 <c>db init</c> 建立 AiNexus 相同（<see cref="DatabaseInitializer.CreateAsync"/>），除非
/// <c>migrate</c> 為 false，否則會套用 migration；測試結束後會刪除。
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
            // 設定變更失敗後也要清除：CREATE 可能已自行成功。
            SqlConnection.ClearAllPools();
            await master.ExecuteAsync($"IF DB_ID(@Name) IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}] END", new { Name = name });
        }
    }

    /// <summary>測試伺服器的 master 資料庫，使用原始測試連線字串，而非 <c>Databases</c> 設定。</summary>
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
