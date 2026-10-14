using AiNexus.Features.AccessControl;
using AiNexus.Features.Artifacts.Export;
using AiNexus.Features.Artifacts.Management;
using AiNexus.Features.Artifacts.Transforms;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Events;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Artifacts;

/// <summary>Versioned documents, their export and text transforms. Other modules create artifacts through <see cref="ArtifactService"/>; each use case has its own file.</summary>
public sealed class ArtifactsModule : IFeatureModule
{
    /// <summary>Largest artifact body (characters) accepted by any artifact endpoint.</summary>
    public const int MaxContentCharacters = 64000;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<ArtifactsDashboard>();
        services.AddScoped<CreateArtifact>();
        services.AddScoped(provider => new ArtifactService(provider.GetRequiredService<CreateArtifact>()));
        services.AddScoped<ArtifactExport>();
        services.AddSingleton<PdfExportRenderer>();
        services.AddSettings<ExportOptions, ExportOptionsValidator>(ExportOptions.Section);
        services.AddFeaturePolicy(FeatureIds.Artifacts);
        services.AddFeaturePolicy(Policies.Text, FeatureIds.Chat, FeatureIds.Artifacts);
        services.AddDomainEventHandler<ContainerDeleted, DetachDeletedProjectArtifacts>();
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/artifacts").RequireAuthorization(Policies.Artifacts).WithTags("Artifacts").WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(MaxContentCharacters));
        ListArtifacts.Map(routes);
        CreateArtifact.Map(routes);
        GetArtifact.Map(routes);
        SaveArtifact.Map(routes);
        DeleteArtifact.Map(routes);
        ListArtifactVersions.Map(routes);
        ReadArtifactAccess.Map(routes);
        SaveArtifactAccess.Map(routes);
        ExportArtifact.Map(routes);
        TransformText.Map(api);
    }
}

internal sealed class ArtifactsFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Artifacts, "成果文件", "/artifacts", 40));
