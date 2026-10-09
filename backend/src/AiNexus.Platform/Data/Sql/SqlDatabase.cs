using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace AiNexus.Platform.Data.Sql;

/// <summary>A database outside EF Core: every call opens its own pooled connection and closes it before returning.</summary>
public sealed class SqlDatabase<TDatabase>(IConfiguration configuration) : DapperSqlDatabase<TDatabase> where TDatabase : ISqlDatabaseDefinition
{
    protected override async Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(TDatabase.ConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);
        return await command(connection, null);
    }
}
