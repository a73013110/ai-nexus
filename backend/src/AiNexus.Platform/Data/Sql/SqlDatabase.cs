using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Data.Sql;

/// <summary>A database outside EF Core: every call opens its own pooled connection and closes it before returning.</summary>
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
