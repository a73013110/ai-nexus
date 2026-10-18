using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using AiNexus.Data.App;
using AiNexus.IntegrationTests.Host;
using AiNexus.Platform.Data.Sql;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 供 <c>Category=SqlServer</c> 測試使用的真實 SQL Server 2025，連線字串來自 <c>AINEXUS_SQLSERVER_TEST</c>：每個測試都會取得一個資料庫，
/// 建立方式與 <c>db init</c> 相同（<see cref="DatabaseInitializer.CreateAsync"/>），再以 <c>database/App</c> 的 dacpac 部署結構；測試結束後會刪除。
/// </summary>
/// <remarks>
/// 每個測試各部署一次 dacpac，比較慢；改成 LocalDB 範本庫是導入計畫的階段 3。
/// dacpac 需先建置（<c>dotnet build database/App</c>，<c>scripts/Verify.ps1</c> 會先做）。
/// </remarks>
internal static class SqlServerDatabase
{
    public static async Task WithDatabase(Func<AppDbContext, Task> test)
    {
        var connection = Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未設定 AINEXUS_SQLSERVER_TEST，略過真實 SQL Server 2025 整合測試。");
        var name = "AINexus_Test_" + Guid.NewGuid().ToString("N");
        var masterConnection = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" }.ConnectionString;
        var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = name };
        await using var master = new SqlConnection(masterConnection); await master.OpenAsync();
        try
        {
            await DatabaseInitializer.CreateAsync(new TestMasterDatabase(masterConnection), name, CancellationToken.None);
            await DeployAsync(settings.ConnectionString);
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(settings.ConnectionString).Options);
            await test(db);
        }
        finally
        {
            // 設定變更失敗後也要清除：CREATE 可能已自行成功。
            SqlConnection.ClearAllPools();
            await master.ExecuteAsync($"IF DB_ID(@Name) IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}] END", new { Name = name });
        }
    }

    /// <summary>以工具清單固定版本的 SqlPackage 發布 dacpac；有全文元件時再依序執行 <c>FullText/</c> 的目錄與索引腳本。</summary>
    private static async Task DeployAsync(string connection)
    {
        var root = OpenApiContractTests.RepositoryRoot();
        var dacpac = new[] { "Release", "Debug" }.Select(x => Path.Combine(root, "database", "App", "bin", x, "App.dacpac"))
            .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            ?? throw new InvalidOperationException("找不到 App.dacpac；先執行 dotnet build database/App。");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "tool", "run", "sqlpackage", "/Action:Publish", "/SourceFile:" + dacpac, "/TargetConnectionString:" + connection })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("dacpac 部署失敗：" + await output + await error);

        await using var db = new SqlConnection(connection);
        await db.OpenAsync();
        if (await db.QuerySingleAsync<int>("SELECT CONVERT(int, SERVERPROPERTY('IsFullTextInstalled'))") != 1) return;
        // 全文語句不能在交易內執行，逐批直接送出。
        foreach (var folder in new[] { "Catalogs", "Indexes" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "database", "App", "FullText", folder), "*.sql").Order(StringComparer.Ordinal))
                foreach (var batch in Regex.Split(await File.ReadAllTextAsync(file), @"^\s*GO\s*$", RegexOptions.Multiline).Where(x => !string.IsNullOrWhiteSpace(x)))
                    await db.ExecuteAsync(batch);
    }

    /// <summary>測試伺服器的 master 資料庫，使用原始測試連線字串，而非 <c>Databases</c> 設定。</summary>
    internal sealed class TestMasterDatabase(string connectionString) : DapperSqlDatabase<MasterDatabase>
    {
        protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return await command(connection, null);
        }
    }
}
