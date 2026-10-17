using AiNexus.Platform.Modules;

namespace AiNexus.Features.Collaboration;

/// <summary>專案、知識、成果與評估共用的資源擁有權、ACL 與生命週期，以及可分享對象的目錄。</summary>
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
