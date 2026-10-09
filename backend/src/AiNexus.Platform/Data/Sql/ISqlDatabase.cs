namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// Hand-written, parameterized SQL (Dapper) against one database. <typeparamref name="TDatabase"/> names the database:
/// a DbContext type for the application database (commands share its connection and current transaction), or an
/// <see cref="ISqlDatabaseDefinition"/> for a database EF Core does not manage (one connection per call).
/// Values always go in <c>parameters</c>; SQL text and object names come only from code.
/// </summary>
public interface ISqlDatabase<TDatabase>
{
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <returns>The number of rows affected.</returns>
    Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);
}
