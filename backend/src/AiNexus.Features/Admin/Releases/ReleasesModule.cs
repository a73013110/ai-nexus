using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Releases.SchemaChanges;
using AiNexus.Features.Admin.Releases.Versions;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Releases;

/// <summary>
/// 版本與結構異動（ADR 0027、0028）：所有使用者看得到執行中的版本與本版更新；版本頁（獨立 grant）看程式與資料庫的版本對照、
/// 與這一版應有結構不同的物件、部署歷史。異動偵測也接到即時監控，並每天檢查一次通知管理者。
/// </summary>
public sealed class ReleasesModule : IFeatureModule
{
    /// <summary>下載異動物件的定義會讀整個資料庫的 metadata，每人每分鐘 2 次。</summary>
    public const string ExportRateLimit = "releases-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSingleton<AppRelease>();
        services.AddScoped<SchemaChangeReader>();
        services.AddHostedService<AppDeploymentRecorder>();
        services.AddHostedService<SchemaChangeWatch>();
        services.AddMonitoringSource<ReleasesMonitoring>();
        services.AddFeaturePolicy(FeatureCatalog.Releases);
        services.AddRateLimiter(options => options.AddPerUserLimit(ExportRateLimit, 2));
    }

    // 端點順序即為已發布的 OpenAPI 順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        GetCurrentRelease.Map(api);
        var admin = api.MapGroup("/admin").RequireAuthorization(Policies.Releases);
        GetReleaseStatus.Map(admin);
        ListDeployments.Map(admin);
        var changes = admin.MapGroup("/schema-changes");
        ListSchemaChanges.Map(changes);
        ExportSchemaChanges.Map(changes);
        GetSchemaChange.Map(changes);
    }
}
