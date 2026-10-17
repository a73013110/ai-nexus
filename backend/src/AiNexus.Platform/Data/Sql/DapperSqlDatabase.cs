using System.Data.Common;
using Dapper;

namespace AiNexus.Platform.Data.Sql;

/// <summary>以 Dapper 執行 <see cref="ISqlDatabase{TDatabase}"/> 命令的基底類別。連線來源（共用的 DbContext 連線或獨立連線）由子類別決定。</summary>
public abstract class DapperSqlDatabase<TDatabase> : ISqlDatabase<TDatabase>
{
    /// <summary>在已開啟的連線上執行 <paramref name="command"/>，並帶入目前應加入的交易（若有）。</summary>
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
