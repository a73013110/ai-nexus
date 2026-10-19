using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Artifacts.Export;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Work.Artifacts.Transforms;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Events;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Artifacts;

/// <summary>有版本的成果文件、匯出與文字轉換。其他模組透過 <see cref="ArtifactService"/> 建立成果；每個 use case 各一個檔案。</summary>
public sealed class ArtifactsModule : IFeatureModule
{
    /// <summary>所有成果端點接受的內文字元數上限。</summary>
    public const int MaxContentCharacters = Artifact.ContentMaxLength;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<ArtifactsDashboard>();
        services.AddScoped<CreateArtifact>();
        services.AddScoped(provider => new ArtifactService(provider.GetRequiredService<CreateArtifact>()));
        services.AddScoped<ArtifactExport>();
        services.AddSingleton<PdfExportRenderer>();
        services.AddSettings<ExportOptions, ExportOptionsValidator>(ExportOptions.Section);
        services.AddFeaturePolicy(FeatureCatalog.Artifacts);
        services.AddFeaturePolicy(Policies.Text, FeatureCatalog.Chat, FeatureCatalog.Artifacts);
        services.AddDomainEventHandler<ContainerDeleted, DetachDeletedProjectArtifacts>();
    }

    // 端點註冊順序即 OpenAPI 發佈順序；調整順序會改變 contracts/openapi.json。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/artifacts")
            .RequireAuthorization(Policies.Artifacts)
            .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(MaxContentCharacters));
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
