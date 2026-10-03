using EDoc.Core.Database.Interfaces;
using System.Data.Common;
using static Dapper.SqlMapper;

namespace EDoc.Core.Database.Implementations;

/// <summary>
/// GridReader 包裝器，實作 IGridReader 介面
/// <para>擁有連線的生命週期：Dispose 時一併釋放連線</para>
/// </summary>
internal sealed class GridReaderWrapper : IGridReader
{
    private readonly GridReader _gridReader;
    private readonly DbConnection _connection;

    public GridReaderWrapper(GridReader gridReader, DbConnection connection)
    {
        _gridReader = gridReader;
        _connection = connection;
    }

    public bool IsConsumed => _gridReader.IsConsumed;

    public IEnumerable<T> Read<T>() => _gridReader.Read<T>();
    public Task<IEnumerable<T>> ReadAsync<T>() => _gridReader.ReadAsync<T>();
    public IEnumerable<dynamic> Read() => _gridReader.Read();
    public Task<IEnumerable<dynamic>> ReadAsync() => _gridReader.ReadAsync();
    public T ReadFirst<T>() => _gridReader.ReadFirst<T>();
    public Task<T> ReadFirstAsync<T>() => _gridReader.ReadFirstAsync<T>();
    public T? ReadFirstOrDefault<T>() => _gridReader.ReadFirstOrDefault<T>();
    public Task<T?> ReadFirstOrDefaultAsync<T>() => _gridReader.ReadFirstOrDefaultAsync<T>();
    public T ReadSingle<T>() => _gridReader.ReadSingle<T>();
    public Task<T> ReadSingleAsync<T>() => _gridReader.ReadSingleAsync<T>();
    public T? ReadSingleOrDefault<T>() => _gridReader.ReadSingleOrDefault<T>();
    public Task<T?> ReadSingleOrDefaultAsync<T>() => _gridReader.ReadSingleOrDefaultAsync<T>();

    public void Dispose()
    {
        _gridReader.Dispose();
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _gridReader.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
