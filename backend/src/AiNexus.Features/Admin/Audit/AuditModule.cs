using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Audit;

/// <summary>
/// 活動稽核：唯讀地查出誰做了什麼。路由在 /admin/audit 下，但權限是獨立功能，不跟 /admin 管理功能綁在一起。
/// </summary>
public sealed class AuditModule : IFeatureModule
{
    public const string Feature = FeatureCatalog.Audit, Policy = Policies.Prefix + Feature;
    public const string ExportRateLimit = "audit-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(Feature);
        builder.Services.AddScoped<AuditDataScope>();
        builder.Services.AddSettings<AuditOptions, AuditOptionsValidator>(AuditOptions.Section);
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(ExportRateLimit, 2));
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        // 保留 API 路徑不變（前端與已發布的文件依賴它），只把授權與 /admin 管理功能分開。
        var audit = api.MapGroup("/admin/audit").RequireAuthorization(Policy);
        ListActivityAudit.Map(audit);
        GetAuditCatalog.Map(audit);
        ExportActivityAudit.Map(audit);
    }
}
