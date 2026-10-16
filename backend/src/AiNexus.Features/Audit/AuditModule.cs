using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Audit;

/// <summary>
/// The activity audit: read-only investigation of who did what. It lives under /admin/audit but is granted separately
/// from /admin management through its own feature. Each use case has its own file.
/// </summary>
public sealed class AuditModule : IFeatureModule
{
    /// <summary>The feature id; <c>NexusDbContext</c> seeds the feature from it.</summary>
    public const string Feature = "audit", Policy = Policies.Prefix + Feature;
    public const string ExportRateLimit = "audit-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(Feature);
        builder.Services.AddSettings<AuditOptions, AuditOptionsValidator>(AuditOptions.Section);
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(ExportRateLimit, 2));
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        // Preserve the API URL while separating its authorization from /admin management.
        var audit = api.MapGroup("/admin/audit").RequireAuthorization(Policy).WithTags("Activity audit");
        ListActivityAudit.Map(audit);
        GetAuditCatalog.Map(audit);
        ExportActivityAudit.Map(audit);
    }
}

internal sealed class AuditFeatures() : FeatureSeed(new PlatformFeature(AuditModule.Feature, "活動稽核", "/admin/audit", 97, BuiltInAccess.SystemNavigation, "audit", AdministratorsOnly: true));
