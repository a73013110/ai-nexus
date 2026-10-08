namespace EDoc.Core.Database.Interfaces;

/// <summary>
/// 多結果集讀取器介面，用於讀取 QueryMultiple 的多個結果集
/// </summary>
/// <remarks>
/// <para>注意：使用完畢後請務必 Dispose 以釋放資料庫連線資源</para>
/// <para>使用範例：</para>
/// <code>
/// using var reader = await _dbHelper.QueryMultipleAsync(sql, param);
/// var users = await reader.ReadAsync<User>();
/// var orders = await reader.ReadAsync<Order>();
/// </code>
/// </remarks>
public interface IGridReader : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 讀取下一個結果集並對映至指定型別
    /// </summary>
    IEnumerable<T> Read<T>();

    /// <summary>
    /// 讀取下一個結果集並對映至指定型別（非同步）
    /// </summary>
    Task<IEnumerable<T>> ReadAsync<T>();

    /// <summary>
    /// 讀取下一個結果集並對映至動態型別
    /// </summary>
    IEnumerable<dynamic> Read();

    /// <summary>
    /// 讀取下一個結果集並對映至動態型別（非同步）
    /// </summary>
    Task<IEnumerable<dynamic>> ReadAsync();

    /// <summary>
    /// 讀取下一個結果集的第一筆資料
    /// </summary>
    T ReadFirst<T>();

    /// <summary>
    /// 讀取下一個結果集的第一筆資料（非同步）
    /// </summary>
    Task<T> ReadFirstAsync<T>();

    /// <summary>
    /// 讀取下一個結果集的第一筆資料，若無資料則傳回預設值
    /// </summary>
    T? ReadFirstOrDefault<T>();

    /// <summary>
    /// 讀取下一個結果集的第一筆資料，若無資料則傳回預設值（非同步）
    /// </summary>
    Task<T?> ReadFirstOrDefaultAsync<T>();

    /// <summary>
    /// 讀取下一個結果集的單一資料（若有多筆會拋出例外）
    /// </summary>
    T ReadSingle<T>();

    /// <summary>
    /// 讀取下一個結果集的單一資料（非同步，若有多筆會拋出例外）
    /// </summary>
    Task<T> ReadSingleAsync<T>();

    /// <summary>
    /// 讀取下一個結果集的單一資料，若無資料則傳回預設值
    /// </summary>
    T? ReadSingleOrDefault<T>();

    /// <summary>
    /// 讀取下一個結果集的單一資料，若無資料則傳回預設值（非同步）
    /// </summary>
    Task<T?> ReadSingleOrDefaultAsync<T>();

    /// <summary>
    /// 是否已讀取完所有結果集
    /// </summary>
    bool IsConsumed { get; }
}
