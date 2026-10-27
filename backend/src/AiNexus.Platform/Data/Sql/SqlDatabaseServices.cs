using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Data.Sql;

/// <summary>SQL 資料庫的註冊擴充方法，提供模組宣告資料庫，以及注入原生 SQL 執行器。</summary>
public static class SqlDatabaseServices
{
    /// <summary>
    /// 宣告模組要使用 <c>Databases:&lt;name&gt;</c> 的連線設定。只有宣告過的項目能通過啟動驗證；
    /// <paramref name="readOnly"/> 為 true 時，對應設定必須有 <c>ReadOnly=true</c>；<paramref name="label"/> 是畫面上的名稱。
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, string name, bool readOnly = false, string? label = null)
    {
        services.PostConfigure<DatabasesOptions>(databases => databases.Declare(name, readOnly, label));
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
