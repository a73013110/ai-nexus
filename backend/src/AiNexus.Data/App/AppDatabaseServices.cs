using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Events;
using AiNexus.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AiNexus.Data.App;

/// <summary>
/// 主資料庫的服務註冊：<see cref="AppDbContext"/>、稽核、SQL 存取、健康檢查與結構檢查。
/// 這是功能模組共同依賴的基礎設施，不是功能模組，所以不實作 <c>IFeatureModule</c>，由 <c>AddFeatures</c> 先行註冊。
/// </summary>
public static class AppDatabaseServices
{
    public static IHostApplicationBuilder AddAppDatabase(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDomainEvents();
        services.AddDatabase(AppDbContext.DatabaseName, label: "主資料庫");

        // 即使尚未設定連線字串也照常註冊，由 UseStorageReadiness 在請求層擋下，而不是讓啟動失敗。
        services.AddDbContext<AppDbContext>((scope, options) => options
            .UseSqlServer(scope.GetRequiredService<IOptions<DatabasesOptions>>().Value.ConnectionString(AppDbContext.DatabaseName))
            .AddAppInterceptors(scope));

        services.AddDbContextSqlDatabase<AppDbContext>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditContextSource>();
        services.AddSingleton<RepeatedReads>();
        services.AddScoped<AuditTrail>();
        services.AddScoped<DatabaseSchema>();
        services.AddScoped<SqlVectorCapabilities>();
        services.AddSingleton<StorageReadiness>();
        services.AddSingleton<DatabaseHealthCheck>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);
        return builder;
    }
}
