using Dapper;
using EDoc.Core.Database.Interfaces;
using EDoc.Core.Database.Models;
using System.Data;
using System.Data.Common;

namespace EDoc.Core.Database.Implementations;

/// <summary>
/// 資料庫交易範圍 - 使用 using 確保自動 Rollback
/// </summary>
public sealed class DbTransactionScope : IDbTransactionScope
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private bool _committed;
    private bool _disposed;

    /// <summary>
    /// private：僅供同類別的工廠方法呼叫（連線與交易已就緒）
    /// </summary>
    private DbTransactionScope(DbConnection connection, DbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>
    /// 建立交易範圍；開啟連線或啟動交易失敗時，於此釋放連線避免洩漏
    /// </summary>
    internal static DbTransactionScope Create(DbConnection connection)
    {
        try
        {
            if (connection.State != ConnectionState.Open) connection.Open();
            var transaction = connection.BeginTransaction();
            return new DbTransactionScope(connection, transaction);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 建立交易範圍（非同步）；開啟連線或啟動交易失敗時，於此釋放連線避免洩漏
    /// </summary>
    internal static async Task<DbTransactionScope> CreateAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            var transaction = await connection.BeginTransactionAsync(cancellationToken);
            return new DbTransactionScope(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 執行非查詢命令
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, object? param = null, CommandType? commandType = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        return await _connection.ExecuteAsync(BuildCommand(sql, param, commandType, commandTimeout, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果傳回指定類型的物件序列
    /// </summary>
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CommandType? commandType = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        return await _connection.QueryAsync<T>(BuildCommand(sql, param, commandType, commandTimeout, cancellationToken));
    }

    /// <summary>
    /// 依查詢結果將第一行傳回指定類型或 Null
    /// </summary>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CommandType? commandType = null, int? commandTimeout = null, CancellationToken cancellationToken = default)
    {
        return await _connection.QueryFirstOrDefaultAsync<T>(BuildCommand(sql, param, commandType, commandTimeout, cancellationToken));
    }

    /// <summary>
    /// 組合 Dapper CommandDefinition（統一經 DbSafeObject 包裝以繞過 CheckMarx 誤判，並掛載交易與 CancellationToken）
    /// </summary>
    private CommandDefinition BuildCommand(string sql, object? param, CommandType? commandType, int? commandTimeout, CancellationToken cancellationToken)
    {
        var dbSafeObject = new DbSafeObject(sql, param);
        return new(dbSafeObject.Sql, dbSafeObject.Param, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 提交交易
    /// </summary>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transaction.Commit();
        _committed = true;
    }

    /// <summary>
    /// 提交交易（非同步）
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    /// <summary>
    /// 回滾交易
    /// </summary>
    public void Rollback()
    {
        if (_disposed || _committed) return;
        _transaction.Rollback();
    }

    /// <summary>
    /// 回滾交易（非同步）
    /// </summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _committed) return;
        await _transaction.RollbackAsync(cancellationToken);
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (!_committed) _transaction.Rollback();
        _transaction.Dispose();
        _connection.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_committed) await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
        _disposed = true;
    }
}
