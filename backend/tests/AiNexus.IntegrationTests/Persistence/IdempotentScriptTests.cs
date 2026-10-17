using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AiNexus.IntegrationTests.Persistence;

/// <summary>
/// 驗證 DBA 在真實 SQL Server 上執行的 SQL（<c>migrations.sql</c>）。測試若逐一指令套用 migration，
/// 會掩蓋批次編譯錯誤，例如同一 migration 中的陳述式引用了較早才新增的欄位。
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class IdempotentScriptTests
{
    [Fact]
    public async Task IdempotentScriptAppliesToAnEmptyDatabaseAndRerunsAsANoOp()
    {
        await SqlServerDatabase.WithDatabase(async db => {
            var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            await RunAsync(db.Database.GetConnectionString()!, script);
            await RunAsync(db.Database.GetConnectionString()!, script);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
        }, migrate: false);
    }

    /// <summary>以 sqlcmd 的方式執行 <paramref name="script"/>：每個 <c>GO</c> 為一批，遇到第一個錯誤即停止。</summary>
    private static async Task RunAsync(string connectionString, string script)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in script.Split(["\r\nGO\r\n", "\nGO\n", "\r\nGO\n"], StringSplitOptions.None).Select(x => x.Trim()).Where(x => x.Length > 0 && x != "GO"))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
    }
}
