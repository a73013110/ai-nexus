using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Data.Sql;

/// <summary>SQL 資料庫服務註冊擴充方法。提供模組資料庫宣告與原生 SQL 資料庫執行個體的相依性注入機制。</summary>
public static class SqlDatabaseServices
{
    /// <summary>
    /// 宣告模組需使用 <c>Databases:&lt;name&gt;</c> 連線設定。僅有經宣告的項目能通過啟動驗證；
    /// 若 <paramref name="readOnly"/> 為 true，則對應設定必須包含 <c>ReadOnly=true</c>。
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, string name, bool readOnly = false)
    {
        services.PostConfigure<DatabasesOptions>(databases => databases.Declare(name, readOnly));
        return services;
    }

    /// <summary>為非 EF Core 管理的獨立資料庫註冊 <see cref="ISqlDatabase{TDatabase}"/>。</summary>
    public static IServiceCollection AddSqlDatabase<TDatabase>(this IServiceCollection services) where TDatabase : ISqlDatabaseDefinition
    {
        services.AddDatabase(TDatabase.Name, TDatabase.ReadOnly);
        services.TryAddSingleton<ISqlDatabase<TDatabase>, SqlDatabase<TDatabase>>();
        return services;
    }

    /// <summary>在已註冊的 DbContext 連線上註冊 <see cref="ISqlDatabase{TDatabase}"/>。</summary>
    public static IServiceCollection AddDbContextSqlDatabase<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.TryAddScoped<ISqlDatabase<TContext>, DbContextSqlDatabase<TContext>>();
        return services;
    }
}
