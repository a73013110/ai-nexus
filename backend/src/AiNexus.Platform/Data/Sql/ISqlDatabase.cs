namespace AiNexus.Platform.Data.Sql;

/// <summary>
/// 透過 Dapper 執行的原生參數化 SQL 存取介面。
/// <typeparamref name="TDatabase"/> 代表目標資料庫：可為應用程式 DbContext 型別（與當前 EF Core 請求共用連線與交易），
/// 或為 <see cref="ISqlDatabaseDefinition"/>（獨立外部資料庫，每次呼叫使用獨立連線池連線）。
/// 所有外部動態數值一律必須透過 <c>parameters</c> 傳遞；SQL 文本與物件名稱僅能來自後端程式碼，徹底杜絕 SQL 注入攻擊。
/// </summary>
public interface ISqlDatabase<TDatabase>
{
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <returns>受影響的資料列筆數。</returns>
    Task<int> ExecuteAsync(string sql, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);
}
