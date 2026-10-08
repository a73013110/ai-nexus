using EDoc.Core.Database.Markers;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace EDoc.Core.Database.Interfaces;

public interface IDbConnectionFactory
{
    /// <summary>
    /// 根據資料庫定義介面建立連線（供 Dapper 使用）
    /// </summary>
    /// <remarks>回傳 <see cref="DbConnection"/>（而非 IDbConnection）以支援 OpenAsync / DisposeAsync</remarks>
    DbConnection CreateConnection<TDb>() where TDb : IDbMarker;

    /// <summary>
    /// 建立 DbContext 配置選項（供 EF Core 使用，自動管理連線）
    /// </summary>
    DbContextOptions<TContext> CreateDbContextOptions<TDb, TContext>()
        where TDb : IDbMarker
        where TContext : DbContext;
}
