using AiNexus.Platform.Modules;

namespace AiNexus.Features.Collaboration;

/// <summary>Resource ownership, ACLs and lifecycle shared by projects, knowledge, artifacts and evaluations, and the directory of people and groups to share them with.</summary>
public sealed class CollaborationModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ResourceWriteLock>();
        builder.Services.AddScoped<ResourceAccess>();
        builder.Services.AddScoped<ResourceLifecycle>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        SearchDirectory.MapUsers(api);
        SearchDirectory.MapGroups(api);
    }
}
