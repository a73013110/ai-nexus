using EDoc.Core.Database.Markers;
using Microsoft.EntityFrameworkCore;

namespace EDoc.Core.Database.Interfaces;

/// <summary>
/// EF Core 助手基礎介面
/// </summary>
/// <typeparam name="TDb">資料庫標記介面（如 IDbDefault、IDbLog）</typeparam>
public interface IEfHelper<TDb> where TDb : IDbMarker 
{
    /// <summary>
    /// 取得 Entity 的 DbSet（供擴充方法使用）
    /// </summary>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    /// <summary>
    /// 儲存所有變更
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}