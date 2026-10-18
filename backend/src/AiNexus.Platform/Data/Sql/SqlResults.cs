using Dapper;

namespace AiNexus.Platform.Data.Sql;

/// <summary>多結果集 procedure 的讀取器：依 procedure 輸出的順序逐一讀取，每個結果集只能讀一次。</summary>
public sealed class SqlResults(SqlMapper.GridReader grid)
{
    public async Task<IReadOnlyList<T>> ReadAsync<T>() => (await grid.ReadAsync<T>()).AsList();

    public Task<T> ReadSingleAsync<T>() => grid.ReadSingleAsync<T>();
}
