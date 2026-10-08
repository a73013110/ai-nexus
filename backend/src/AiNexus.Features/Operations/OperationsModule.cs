using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Operations;

/// <summary>Service status, the activity audit, background jobs and their workers. Each use case has its own file.</summary>
public sealed class OperationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<JobService>();
        builder.Services.AddScoped<CancelJob>();
        builder.Services.AddScoped<RetryJob>();
        builder.Services.AddScoped<ListActivityAudit>();
        builder.Services.AddHostedService<BackgroundJobWorker>();
        builder.Services.AddHostedService<EventRetentionWorker>();
        builder.Services.AddFeaturePolicy(FeatureIds.Tasks);
        builder.Services.AddFeaturePolicy(ActivityAuditEndpoints.Feature);
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        GetStatus.Map(api);
        // Preserve the API URL while separating its authorization from /admin management.
        var audit = api.MapGroup("/admin/audit").RequireAuthorization(ActivityAuditEndpoints.Policy).WithTags("Activity audit");
        ListActivityAudit.Map(audit);
        GetAuditCatalog.Map(audit);
        var jobs = api.MapGroup("/jobs").WithTags("Background jobs");
        ListJobs.Map(jobs);
        GetJob.Map(jobs);
        CancelJob.Map(jobs);
        RetryJob.Map(jobs);
    }
}
