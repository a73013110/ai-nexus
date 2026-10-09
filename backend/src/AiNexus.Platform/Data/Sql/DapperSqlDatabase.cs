using System.Data.Common;
using Dapper;

namespace AiNexus.Platform.Data.Sql;

/// <summary>Runs <see cref="ISqlDatabase{TDatabase}"/> commands through Dapper; subclasses decide where the connection comes from.</summary>
public abstract class DapperSqlDatabase<TDatabase> : ISqlDatabase<TDatabase>
{
    /// <summary>Runs <paramref name="command"/> on an open connection with the transaction it must join (if any).</summary>
    protected abstract Task<TResult> RunAsync<TResult>(Func<DbConnection, DbTransaction?, Task<TResult>> command, CancellationToken cancellationToken);

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<T>>(async (connection, transaction) =>
            (await connection.QueryAsync<T>(Command(sql, parameters, transaction, commandTimeout, cancellationToken))).AsList(), cancellationToken);

    public Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync((connection, transaction) => connection.QuerySingleAsync<T>(Command(sql, parameters, transaction, commandTimeout, cancellationToken)), cancellationToken);

    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync((connection, transaction) => connection.QuerySingleOrDefaultAsync<T?>(Command(sql, parameters, transaction, commandTimeout, cancellationToken)), cancellationToken);

    public Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
        => RunAsync((connection, transaction) => connection.ExecuteAsync(Command(sql, parameters, transaction, commandTimeout, cancellationToken)), cancellationToken);

    private static CommandDefinition Command(string sql, object? parameters, DbTransaction? transaction, int? commandTimeout, CancellationToken cancellationToken)
        => new(sql, parameters, transaction, commandTimeout, cancellationToken: cancellationToken);
}
