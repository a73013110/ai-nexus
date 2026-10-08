using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Operations;

public sealed class OperationsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<JobService>();
        builder.Services.AddHostedService<BackgroundJobWorker>();
        builder.Services.AddHostedService<EventRetentionWorker>();
        builder.Services.AddFeaturePolicy("tasks");
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapOperations();
        api.MapJobs();
    }
}
