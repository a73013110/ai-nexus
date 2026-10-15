using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AiNexus.IntegrationTests.Persistence;

/// <summary>
/// The SQL a DBA runs (<c>migrations.sql</c>) on a real SQL Server: tests apply migrations one command at a time, which
/// hides batch compile errors such as a statement naming a column added earlier in the same migration.
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

    /// <summary>Runs <paramref name="script"/> the way sqlcmd does: one batch per <c>GO</c>, stopping at the first error.</summary>
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
