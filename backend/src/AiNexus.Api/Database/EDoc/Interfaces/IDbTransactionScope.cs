using System.Data;

namespace EDoc.Core.Database.Interfaces;

/// <summary>
/// 資料庫交易範圍介面，提供在同一交易中執行多個資料庫操作的功能。
/// </summary>
/// <remarks>
/// <para>使用此介面可確保多個資料庫操作在同一交易中執行，支援交易提交與回滾。</para>
/// <para>使用完畢後請務必呼叫 <see cref="CommitAsync"/>（或 <see cref="Commit"/>）提交交易；未提交時 Dispose 會自動回滾。</para>
/// </remarks>
/// <example>
/// <code>
/// await using var scope = await dbHelper.BeginTransactionAsync(cancellationToken);
/// await scope.ExecuteAsync("INSERT INTO ...", new { ... }, cancellationToken: cancellationToken);
/// await scope.ExecuteAsync("UPDATE ...", new { ... }, cancellationToken: cancellationToken);
/// await scope.CommitAsync(cancellationToken);
/// </code>
/// </example>
public interface IDbTransactionScope : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 在交易範圍內執行 SQL 命令（INSERT、UPDATE、DELETE 等）。
    /// </summary>
    /// <param name="sql">要執行的 SQL 語句或預存程序名稱。</param>
    /// <param name="param">SQL 參數物件，屬性名稱會對應到 SQL 中的參數。</param>
    /// <param name="commandType">命令類型，預設為 <see cref="CommandType.Text"/>。若執行預存程序請指定 <see cref="CommandType.StoredProcedure"/>。</param>
    /// <param name="commandTimeout">命令逾時時間（秒），null 表示使用預設值。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>受影響的資料列數。</returns>
    Task<int> ExecuteAsync(string sql, object? param = null, CommandType? commandType = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在交易範圍內執行查詢，並將結果對映至指定型別的集合。
    /// </summary>
    /// <typeparam name="T">結果對映的目標型別。</typeparam>
    /// <param name="sql">要執行的 SQL 查詢語句或預存程序名稱。</param>
    /// <param name="param">SQL 參數物件，屬性名稱會對應到 SQL 中的參數。</param>
    /// <param name="commandType">命令類型，預設為 <see cref="CommandType.Text"/>。若執行預存程序請指定 <see cref="CommandType.StoredProcedure"/>。</param>
    /// <param name="commandTimeout">命令逾時時間（秒），null 表示使用預設值。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>查詢結果的集合。</returns>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CommandType? commandType = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在交易範圍內執行查詢，並傳回第一筆結果或預設值。
    /// </summary>
    /// <typeparam name="T">結果對映的目標型別。</typeparam>
    /// <param name="sql">要執行的 SQL 查詢語句或預存程序名稱。</param>
    /// <param name="param">SQL 參數物件，屬性名稱會對應到 SQL 中的參數。</param>
    /// <param name="commandType">命令類型，預設為 <see cref="CommandType.Text"/>。若執行預存程序請指定 <see cref="CommandType.StoredProcedure"/>。</param>
    /// <param name="commandTimeout">命令逾時時間（秒），null 表示使用預設值。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>第一筆查詢結果，若無結果則傳回 <c>null</c>。</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CommandType? commandType = null, int? commandTimeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交交易，將所有變更永久保存至資料庫。
    /// </summary>
    /// <remarks>
    /// 一旦呼叫此方法，交易中的所有操作將被永久保存，無法再回滾。
    /// </remarks>
    void Commit();

    /// <summary>
    /// 提交交易，將所有變更永久保存至資料庫（非同步）。
    /// </summary>
    /// <remarks>
    /// 一旦呼叫此方法，交易中的所有操作將被永久保存，無法再回滾。
    /// </remarks>
    /// <param name="cancellationToken">取消權杖。</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滾交易，撤銷所有尚未提交的變更。
    /// </summary>
    /// <remarks>
    /// 呼叫此方法後，交易中的所有操作將被取消，資料庫狀態恢復到交易開始前。
    /// </remarks>
    void Rollback();

    /// <summary>
    /// 回滾交易，撤銷所有尚未提交的變更（非同步）。
    /// </summary>
    /// <remarks>
    /// 呼叫此方法後，交易中的所有操作將被取消，資料庫狀態恢復到交易開始前。
    /// </remarks>
    /// <param name="cancellationToken">取消權杖。</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
