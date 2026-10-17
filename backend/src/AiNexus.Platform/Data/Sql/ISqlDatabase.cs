namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// 以 Dapper 執行的原生參數化 SQL 存取介面。
/// <typeparamref name="TDatabase"/> 是目標資料庫：可以是應用程式的 DbContext 型別（與當前 EF Core 請求共用連線與交易），
/// 也可以是 <see cref="ISqlDatabaseDefinition"/>（獨立的外部資料庫，每次呼叫使用獨立連線）。
/// 外部傳入的值一律走 <c>parameters</c>；SQL 文字與物件名稱只能來自後端程式碼，以杜絕 SQL 注入。
/// </summary>
public interface ISqlDatabase<TDatabase>
{
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <returns>受影響的資料列筆數。</returns>
    Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);
}
