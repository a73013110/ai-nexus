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

    /// <summary>
    /// 呼叫不寫稽核的 procedure（<c>schema.usp_&lt;UseCase&gt;</c>），回傳第一個結果集。不帶稽核情境。
    /// 用於讀取，以及背景工作只改處理狀態的認領（例如郵件 outbox）；業務異動用 <c>ExecuteProcedureAsync</c>。
    /// </summary>
    Task<IReadOnlyList<T>> QueryProcedureAsync<T>(string procedure, object? parameters = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>呼叫只讀取、回傳多個結果集的 procedure；<paramref name="read"/> 依序讀出每個結果集。不帶稽核情境。</summary>
    Task<TResult> QueryProcedureAsync<TResult>(string procedure, object? parameters, Func<SqlResults, Task<TResult>> read, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 呼叫會寫入的 procedure，並在同一個批次先設定稽核情境（ADR 0014），回傳 procedure 的第一個結果集（沒有則為空）。
    /// <paramref name="parameters"/> 是匿名物件或 <c>DynamicParameters</c>，只支援輸入參數，結果用 <c>SELECT</c> 回傳；
    /// <paramref name="auditOwner"/> 是稽核的有效身分，請求內省略（取目前使用者），背景工作傳作業擁有者。
    /// </summary>
    /// <exception cref="Microsoft.Data.SqlClient.SqlException">procedure 失敗；預期內的失敗用 <see cref="ProcedureError.Code"/> 取出錯誤代碼。</exception>
    Task<IReadOnlyList<T>> ExecuteProcedureAsync<T>(string procedure, object? parameters = null, Guid? auditOwner = null, int? commandTimeout = null, CancellationToken cancellationToken = default);
}
