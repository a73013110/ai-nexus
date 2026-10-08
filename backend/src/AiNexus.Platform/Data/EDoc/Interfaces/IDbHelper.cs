using EDoc.Core.Database.Markers;
using System.Data;

namespace EDoc.Core.Database.Interfaces;

/// <summary>
/// 資料庫助手介面
/// <para>如需交易支援，請使用 <see cref="BeginTransaction"/> 取得 <see cref="IDbTransactionScope"/></para>
/// <para>查詢結果一律緩衝至記憶體（連線於方法返回前關閉）；如需串流大量資料請另循 IDataReader 途徑</para>
/// </summary>
/// <typeparam name="TDb">資料庫標記介面（如 IDbDefault、IDbLog）</typeparam>
public interface IDbHelper<TDb> where TDb : IDbMarker
{
    /// <summary>
    /// 執行非查詢命令
    /// </summary>
    int Execute(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 執行非查詢命令（非同步）
    /// </summary>
    Task<int> ExecuteAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 傳回查詢結果的 DataTable
    /// </summary>
    DataTable ExecuteReader(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 傳回查詢結果的 DataTable（非同步）
    /// </summary>
    Task<DataTable> ExecuteReaderAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果傳回動態類型的序列
    /// </summary>
    IEnumerable<dynamic> Query(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果傳回指定類型的物件序列
    /// </summary>
    IEnumerable<T> Query<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果傳回動態類型的序列（非同步）
    /// </summary>
    Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果傳回指定類型的物件序列（非同步）
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 執行查詢並返回多結果集讀取器
    /// </summary>
    /// <remarks>
    /// <para>注意：回傳的 IGridReader 需自行使用 using 管理以釋放資源</para>
    /// <para>使用範例：</para>
    /// <code>
    /// using var reader = _dbHelper.QueryMultiple("SELECT * FROM Users; SELECT * FROM Orders");
    /// var users = reader.Read&lt;User&gt;().ToList();
    /// var orders = reader.Read&lt;Order&gt;().ToList();
    /// </code>
    /// </remarks>
    IGridReader QueryMultiple(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 執行查詢並返回多結果集讀取器（非同步）
    /// </summary>
    /// <remarks>
    /// <para>注意：回傳的 IGridReader 需自行使用 await using 管理以釋放資源</para>
    /// <para>使用範例：</para>
    /// <code>
    /// await using var reader = await _dbHelper.QueryMultipleAsync("SELECT * FROM Users; SELECT * FROM Orders");
    /// var users = (await reader.ReadAsync&lt;User&gt;()).ToList();
    /// var orders = (await reader.ReadAsync&lt;Order&gt;()).ToList();
    /// </code>
    /// </remarks>
    Task<IGridReader> QueryMultipleAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型
    /// </summary>
    dynamic QueryFirst(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型
    /// </summary>
    T QueryFirst<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型（非同步）
    /// </summary>
    Task<dynamic> QueryFirstAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型（非同步）
    /// </summary>
    Task<T> QueryFirstAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型或 Null
    /// </summary>
    dynamic? QueryFirstOrDefault(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型或 Null
    /// </summary>
    T? QueryFirstOrDefault<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型或 Null（非同步）
    /// </summary>
    Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型或 Null（非同步）
    /// </summary>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果傳回動態類型（僅限單一結果）
    /// </summary>
    dynamic QuerySingle(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果傳回指定類型（僅限單一結果）
    /// </summary>
    T QuerySingle<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果傳回動態類型（僅限單一結果，非同步）
    /// </summary>
    Task<dynamic> QuerySingleAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果傳回指定類型（僅限單一結果，非同步）
    /// </summary>
    Task<T> QuerySingleAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果傳回動態類型或 Null（僅限單一結果）
    /// </summary>
    dynamic? QuerySingleOrDefault(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果傳回指定類型或 Null（僅限單一結果）
    /// </summary>
    T? QuerySingleOrDefault<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null);

    /// <summary>
    /// 依查詢結果傳回動態類型或 Null（僅限單一結果，非同步）
    /// </summary>
    Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 依查詢結果傳回指定類型或 Null（僅限單一結果，非同步）
    /// </summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 開始交易範圍
    /// </summary>
    /// <remarks>
    /// <para>使用範例：</para>
    /// <code>
    /// // 方式一：使用 try-catch（推薦）
    /// using var scope = _dbHelper.BeginTransaction();
    /// try
    /// {
    ///     await scope.ExecuteAsync("INSERT INTO Orders ...", order);
    ///     await scope.ExecuteAsync("UPDATE Inventory ...", inventory);
    ///     scope.Commit(); // 明確提交
    /// }
    /// catch
    /// {
    ///     // 發生例外時，Dispose 會自動 Rollback
    ///     throw;
    /// }
    ///
    /// // 方式二：簡潔寫法（適用於不需要額外例外處理的情況）
    /// using var scope = _dbHelper.BeginTransaction();
    /// await scope.ExecuteAsync("INSERT INTO Orders ...", order);
    /// await scope.ExecuteAsync("UPDATE Inventory ...", inventory);
    /// scope.Commit(); // 若未呼叫 Commit，Dispose 時會自動 Rollback
    /// </code>
    /// </remarks>
    /// <returns>交易範圍物件，使用完畢後請確保 Dispose</returns>
    IDbTransactionScope BeginTransaction();

    /// <summary>
    /// 開始交易範圍（非同步：以 OpenAsync 開啟連線、BeginTransactionAsync 啟動交易）
    /// </summary>
    /// <remarks>
    /// <para>使用範例：</para>
    /// <code>
    /// await using var scope = await _dbHelper.BeginTransactionAsync(cancellationToken);
    /// await scope.ExecuteAsync("INSERT INTO Orders ...", order, cancellationToken: cancellationToken);
    /// await scope.ExecuteAsync("UPDATE Inventory ...", inventory, cancellationToken: cancellationToken);
    /// await scope.CommitAsync(cancellationToken); // 若未呼叫 Commit，Dispose 時會自動 Rollback
    /// </code>
    /// </remarks>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>交易範圍物件，使用完畢後請確保 Dispose</returns>
    Task<IDbTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
