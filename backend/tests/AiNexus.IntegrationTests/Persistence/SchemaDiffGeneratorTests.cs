using AiNexus.Features.Persistence;
using AiNexus.Features.Persistence.ReleaseScripts;
using AiNexus.Platform.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Persistence;

/// <summary>
/// 暫存資料庫位於共用伺服器上，因此不論建立後發生什麼事，都必須被清除。正常路徑
/// 需要 SqlPackage，並由 <c>Publish-IIS.ps1 -FromMigration</c> 端對端驗證（見 docs/operations/iis-deployment.md）。
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class SchemaDiffGeneratorTests : IDisposable
{
    private readonly DirectoryInfo output = Directory.CreateTempSubdirectory("schema-diff-generator-");

    public void Dispose() => output.Delete(recursive: true);

    [Fact]
    public async Task TemporaryDatabaseIsDroppedWhenSqlPackageFails()
    {
        var connection = Environment.GetEnvironmentVariable("AINEXUS_SQLSERVER_TEST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "未設定 AINEXUS_SQLSERVER_TEST，略過真實 SQL Server 2025 整合測試。");
        var sql = new SqlConnectionStringBuilder(connection);
        var databases = new DatabasesOptions
        {
            [NexusDbContext.DatabaseName] = new()
            {
                Server = sql.DataSource, Name = "AiNexus", IntegratedSecurity = sql.IntegratedSecurity,
                User = sql.IntegratedSecurity ? "" : sql.UserID, Password = sql.IntegratedSecurity ? "" : sql.Password,
                TrustServerCertificate = sql.TrustServerCertificate,
            },
        };
        var master = new SqlServerDatabase.MasterDatabase(new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" }.ConnectionString);
        const string Temporary = "SELECT name FROM sys.databases WHERE name LIKE 'AiNexus[_]Snapshot[_]%'";
        var existing = await master.QueryAsync<string>(Temporary);

        var error = await Assert.ThrowsAsync<ReleaseScriptException>(() => new SchemaDiffGenerator(Options.Create(databases), master)
            .GenerateAsync("0", null, output.FullName, new SqlPackage(Path.Combine(output.FullName, "missing", "SqlPackage.exe")), CancellationToken.None));

        Assert.Contains("SqlPackage", error.Message, StringComparison.Ordinal);
        Assert.Equal(existing, await master.QueryAsync<string>(Temporary));
        Assert.False(File.Exists(Path.Combine(output.FullName, SchemaDiffGenerator.ExpectedDacpacFileName)));
    }
}
