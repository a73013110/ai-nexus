using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Jobs;

/// <summary>
/// Durable background jobs: other modules enqueue work with <see cref="JobService"/> and implement
/// <see cref="IBackgroundJobHandler"/>; the worker leases, runs, fences checkpoints and records the outcome. Each use case
/// has its own file.
/// </summary>
public sealed class JobsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<JobService>();
        builder.Services.AddScoped<CancelJob>();
        builder.Services.AddScoped<RetryJob>();
        builder.Services.AddHostedService<BackgroundJobWorker>();
        builder.Services.AddFeaturePolicy(FeatureIds.Tasks);
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var jobs = api.MapGroup("/jobs").WithTags("Background jobs");
        ListJobs.Map(jobs);
        GetJob.Map(jobs);
        CancelJob.Map(jobs);
        RetryJob.Map(jobs);
    }
}

internal sealed class JobsFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Tasks, "背景任務", "/tasks", 70));
