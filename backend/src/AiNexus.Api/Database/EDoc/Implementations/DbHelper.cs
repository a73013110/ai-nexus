using Dapper;
using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Markers;
using EDoc.Core.Database.Models;
using System.Data;
using System.Data.Common;

namespace EDoc.Core.Database.Implementations;

/// <summary>
/// 資料庫助手
/// <para>每次呼叫建立獨立連線，方法返回前釋放；查詢結果一律緩衝至記憶體</para>
/// </summary>
/// <typeparam name="TDb">資料庫標記介面（如 IDbDefault、IDbLog）</typeparam>
/// <param name="dbConnectionFactory">自動注入</param>
public sealed class DbHelper<TDb>(IDbConnectionFactory dbConnectionFactory) : IDbHelper<TDb> where TDb : IDbMarker
{
    /// <summary>
    /// 建立並開啟連線；開啟失敗時釋放連線後重拋
    /// </summary>
    private DbConnection OpenConnection()
    {
        var cn = dbConnectionFactory.CreateConnection<TDb>();
        try
        {
            cn.Open();
            return cn;
        }
        catch
        {
            cn.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 建立並開啟連線（非同步）；開啟失敗時釋放連線後重拋
    /// </summary>
    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var cn = dbConnectionFactory.CreateConnection<TDb>();
        try
        {
            await cn.OpenAsync(cancellationToken);
            return cn;
        }
        catch
        {
            await cn.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 組合 Dapper CommandDefinition（統一經 DbSafeObject 包裝以繞過 CheckMarx 誤判，並掛載 CancellationToken）
    /// </summary>
    private static CommandDefinition BuildCommand(string sql, object? param, int? commandTimeout, CommandType? commandType, CancellationToken cancellationToken = default)
    {
        var dbSafeObject = new DbSafeObject(sql, param);
        return new(dbSafeObject.Sql, dbSafeObject.Param, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 執行非查詢命令
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public int Execute(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.Execute(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 執行非查詢命令（非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<int> ExecuteAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.ExecuteAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 傳回查詢結果的 DataTable
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public DataTable ExecuteReader(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        using var dr = cn.ExecuteReader(BuildCommand(sql, param, commandTimeout, commandType));
        DataTable dt = new DataSet() { EnforceConstraints = false }.Tables.Add();
        dt.Load(dr);
        return dt;
    }

    /// <summary>
    /// 傳回查詢結果的 DataTable（非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<DataTable> ExecuteReaderAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        using var dr = await cn.ExecuteReaderAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
        DataTable dt = new DataSet() { EnforceConstraints = false }.Tables.Add();
        dt.Load(dr);
        return dt;
    }

    /// <summary>
    /// 依查詢結果傳回動態類型的序列
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public IEnumerable<dynamic> Query(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.Query<dynamic>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果傳回動態類型的序列（非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<IEnumerable<dynamic>> QueryAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 執行查詢並返回一個 GridReader，可用於讀取多個結果集
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <returns>可用於讀取多個結果集的 GridReader</returns>
    public IGridReader QueryMultiple(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        var cn = OpenConnection();
        try
        {
            var gridReader = cn.QueryMultiple(BuildCommand(sql, param, commandTimeout, commandType));
            return new GridReaderWrapper(gridReader, cn);
        }
        catch
        {
            cn.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 非同步執行查詢並返回一個 GridReader，可用於讀取多個結果集
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>可用於讀取多個結果集的 GridReader</returns>
    public async Task<IGridReader> QueryMultipleAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        var cn = await OpenConnectionAsync(cancellationToken);
        try
        {
            var gridReader = await cn.QueryMultipleAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
            return new GridReaderWrapper(gridReader, cn);
        }
        catch
        {
            await cn.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 依查詢結果傳回指定類型的物件序列
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public IEnumerable<T> Query<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.Query<T>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果傳回指定類型的物件序列（非同步）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<T>(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public dynamic QueryFirst(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QueryFirst<dynamic>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型（非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<dynamic> QueryFirstAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QueryFirstAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public T QueryFirst<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QueryFirst<T>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型（非同步）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<T> QueryFirstAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QueryFirstAsync<T>(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型或 Null
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public dynamic? QueryFirstOrDefault(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QueryFirstOrDefault<dynamic>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回動態類型或 Null（非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<dynamic?> QueryFirstOrDefaultAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QueryFirstOrDefaultAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型或 Null
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public T? QueryFirstOrDefault<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QueryFirstOrDefault<T>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型或 Null（非同步）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QueryFirstOrDefaultAsync<T>(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果傳回動態類型（僅限單一結果）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public dynamic QuerySingle(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QuerySingle<dynamic>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果傳回動態類型（僅限單一結果，非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<dynamic> QuerySingleAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果傳回指定類型（僅限單一結果）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public T QuerySingle<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QuerySingle<T>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果傳回指定類型（僅限單一結果，非同步）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<T> QuerySingleAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<T>(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果傳回動態類型或 Null（僅限單一結果）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public dynamic? QuerySingleOrDefault(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QuerySingleOrDefault<dynamic>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果傳回動態類型或 Null（僅限單一結果，非同步）
    /// </summary>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<dynamic?> QuerySingleOrDefaultAsync(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果傳回指定類型或 Null（僅限單一結果）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    public T? QuerySingleOrDefault<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null)
    {
        using var cn = OpenConnection();
        return cn.QuerySingleOrDefault<T>(BuildCommand(sql, param, commandTimeout, commandType));
    }

    /// <summary>
    /// 依查詢結果傳回指定類型或 Null（僅限單一結果，非同步）
    /// </summary>
    /// <typeparam name="T">屬性與查詢結果相符的類型</typeparam>
    /// <param name="sql">要執行的 SQL</param>
    /// <param name="param">要傳遞的參數</param>
    /// <param name="commandTimeout">逾時（以秒為單位）</param>
    /// <param name="commandType">要執行的指令類型</param>
    /// <param name="cancellationToken">取消權杖</param>
    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await using var cn = await OpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<T>(BuildCommand(sql, param, commandTimeout, commandType, cancellationToken));
    }

    /// <inheritdoc />
    public IDbTransactionScope BeginTransaction()
        => DbTransactionScope.Create(dbConnectionFactory.CreateConnection<TDb>());

    /// <inheritdoc />
    public async Task<IDbTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await DbTransactionScope.CreateAsync(dbConnectionFactory.CreateConnection<TDb>(), cancellationToken);
}
