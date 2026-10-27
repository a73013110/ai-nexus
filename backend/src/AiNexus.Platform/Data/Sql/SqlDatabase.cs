using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Data.Sql;

/// <summary>非 EF Core 管理的獨立資料庫執行器。每次呼叫都從連線池取得連線，呼叫結束即釋放回連線池。</summary>
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
