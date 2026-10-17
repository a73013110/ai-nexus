using AiNexus.Features.Persistence.ReleaseScripts;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using AiNexus.Platform.Health;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence;

/// <summary>
/// 核心資料庫持久化模組。
/// </summary>
/// <remarks>
/// 職責說明：
/// 1. 註冊 EF Core <see cref="NexusDbContext"/> 與連線字串解析。
/// 2. 註冊審計日誌攔截器 (<see cref="AuditEventInterceptor"/>) 與領域事件發布攔截器 (<see cref="DomainEventInterceptor"/>)。
/// 3. 註冊資料庫健康檢查 (<see cref="DatabaseHealthCheck"/>) 與就緒性探針（Ready Tag）。
/// 4. 註冊資料庫結構差異比對工具 (<see cref="SchemaDiffGenerator"/>) 與向量功能相容性檢測 (<see cref="SqlVectorCapabilities"/>)。
/// </remarks>
public sealed class PersistenceModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDomainEvents();
        services.AddSettings<PersistenceOptions, PersistenceOptionsValidator>(PersistenceOptions.Section);
        services.AddDatabase(NexusDbContext.DatabaseName);

        // 即使尚未設定連線字串也照常註冊，由 UseStorageReadiness 在請求層擋下，而不是讓啟動失敗。
        services.AddDbContext<NexusDbContext>((scope, options) => options
            .UseSqlServer(scope.GetRequiredService<IOptions<DatabasesOptions>>().Value.ConnectionString(NexusDbContext.DatabaseName))
            .AddNexusInterceptors(scope));

        services.AddDbContextSqlDatabase<NexusDbContext>();
        services.AddSqlDatabase<NexusMasterDatabase>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<SchemaDiffGenerator>();
        services.AddScoped<DatabaseSchema>();
        services.AddScoped<SqlVectorCapabilities>();
        services.AddSingleton<StorageReadiness>();
        services.AddSingleton<DatabaseHealthCheck>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag]);
    }
}

/// <summary>
/// 資料庫啟動階段準備與健康檢測擴充方法。
/// </summary>
public static class DatabaseStartup
{
    /// <summary>
    /// Nexus 連線尚未設定完成時，除認證端點外的 API 請求直接回傳 503，避免每個請求都去連一個不存在的資料庫而逾時。
    /// </summary>
    public static IApplicationBuilder UseStorageReadiness(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        // 認證端點放行，讓管理員仍能登入並完成連線設定。
        if (http.Request.Path.StartsWithSegments("/api/v1") && !http.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            http.RequestServices.GetRequiredService<StorageReadiness>().RequireConfigured();
        }

        await next(http);
    });

    /// <summary>
    /// 在背景服務與 API 請求開始前檢查資料庫結構，並依設定套用 SQL Server migrations。
    /// SQLite 測試環境不走這條路徑，模型由測試自行建立。
    /// </summary>
    /// <returns>若資料庫結構無法使用則回傳 <see langword="false"/>，呼叫端應立即停止 Host 啟動。</returns>
    public static async Task<bool> EnsureDatabaseReadyAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<StorageReadiness>();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var migrate = scope.ServiceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value.ApplyMigrationsOnStartup;

        if (migrate)
        {
            storage.RequireConfigured();
        }

        // SQLite 測試資料庫不跑 SQL Server migrations，這裡直接略過。
        if (!storage.Configured || !db.Database.IsSqlServer())
        {
            return true;
        }

        try
        {
            if (migrate)
            {
                await db.Database.MigrateAsync();
            }

            var schema = scope.ServiceProvider.GetRequiredService<DatabaseSchema>();
            await schema.RequireCurrentAsync(CancellationToken.None);

            // RCSI 只檢查並記錄警告，不在此切換：切換需要排他鎖，必須由 DBA 於維護期間手動執行。
            await schema.ReadCommittedSnapshotAsync(CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            await DiagnosticStartup.RecordAsync(ex, app.Configuration, app.Environment);
            Console.Error.WriteLine(ex is ExternalServiceException external
                ? external.Message
                : DatabaseDiagnosis.Describe(ex, NexusDbContext.DatabaseName));
            return false;
        }
    }
}
