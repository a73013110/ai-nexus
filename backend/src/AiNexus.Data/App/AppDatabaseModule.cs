using AiNexus.Platform.Auditing;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Events;
using AiNexus.Platform.Health;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AiNexus.Data.App;

/// <summary>主資料庫的服務註冊：<see cref="AppDbContext"/>、稽核、SQL 存取、健康檢查與結構檢查。</summary>
public sealed class AppDatabaseModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
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
    }
}
