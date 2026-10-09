using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Platform.Events;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Artifacts;

/// <summary>Versioned documents, their export and text transforms. Other modules create artifacts through <see cref="ArtifactService"/>; each use case has its own file.</summary>
public sealed class ArtifactsModule : IFeatureModule
{
    /// <summary>Largest artifact body (characters) accepted by any artifact endpoint.</summary>
    public const int MaxContentCharacters = 64000;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<GetArtifact>();
        services.AddScoped<CreateArtifact>();
        services.AddScoped<SaveArtifact>();
        services.AddScoped<ExportArtifact>();
        services.AddScoped<TransformText>();
        services.AddScoped(provider => new ArtifactService(provider.GetRequiredService<CreateArtifact>()));
        services.AddScoped<ArtifactExport>();
        services.AddSingleton<PdfExportRenderer>();
        services.AddOptions<ExportOptions>().BindConfiguration("Exports").ValidateOnStart();
        services.AddSingleton<IValidateOptions<ExportOptions>, ExportOptionsValidator>();
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

internal sealed class ExportOptionsValidator : IValidateOptions<ExportOptions>
{
    public ValidateOptionsResult Validate(string? name, ExportOptions x)
        => x.BrowserChannel is "msedge" or "chrome" or "chromium" && x.TimeoutSeconds is >= 5 and <= 120
           && (string.IsNullOrWhiteSpace(x.BrowserExecutablePath) || Path.IsPathFullyQualified(x.BrowserExecutablePath))
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid document export browser settings.");
}
