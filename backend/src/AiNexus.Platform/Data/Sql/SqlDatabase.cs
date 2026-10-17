using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Data.Sql;

/// <summary>非 EF Core 管理的獨立 SQL 資料庫執行器：每次呼叫從連線池取得獨立連線，並在呼叫結束時自動釋放歸還連線池。</summary>
public sealed class SqlDatabase<TDatabase>(IOptions<DatabasesOptions> databases) : DapperSqlDatabase<TDatabase> where TDatabase : ISqlDatabaseDefinition
{
    protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
    {
        var connectionString = databases.Value.ConnectionString(TDatabase.Name, TDatabase.Catalog);
        if (connectionString.Length == 0) throw new InvalidOperationException($"{DatabasesOptions.Section}:{TDatabase.Name} is not configured.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await command(connection, null);
    }
}
