using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Projects;

public sealed class ProjectsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ProjectService>();
        builder.Services.AddFeaturePolicy("projects");
    }

    public static void MapEndpoints(RouteGroupBuilder api) => ProjectEndpoints.MapProjects(api);
}
