using AiNexus.Features.Persistence;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// Real SQL Server 2025 for <c>Category=SqlServer</c> tests, from <c>AINEXUS_SQLSERVER_TEST</c>: each test gets a freshly
/// migrated database that is dropped afterwards.
/// </summary>
internal static class SqlServerDatabase
{
    public static async Task WithDatabase(Func<NexusDbContext, Task> test)
    {
        var connection = Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未設定 AINEXUS_SQLSERVER_TEST，略過真實 SQL Server 2025 整合測試。");
        var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        var name = "AINexus_Test_" + Guid.NewGuid().ToString("N"); var created = false;
        await using var master = new SqlConnection(settings.ConnectionString); await master.OpenAsync();
        try
        {
            await master.ExecuteAsync($"CREATE DATABASE [{name}]"); created = true; settings.InitialCatalog = name;
            await using var db = new NexusDbContext(new DbContextOptionsBuilder<NexusDbContext>().UseSqlServer(settings.ConnectionString).Options);
            await db.Database.MigrateAsync();
            await test(db);
        }
        finally
        {
            if (created) { SqlConnection.ClearAllPools(); await master.ExecuteAsync($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]"); }
        }
    }
}
