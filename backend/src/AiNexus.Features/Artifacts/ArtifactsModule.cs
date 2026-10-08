using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Artifacts;

public sealed class ArtifactsModule : IFeatureModule
{
    /// <summary>Largest artifact body (characters) accepted by any artifact endpoint.</summary>
    public const int MaxContentCharacters = 64000;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<ArtifactService>();
        services.AddScoped<TextTransformService>();
        services.AddScoped<ArtifactExport>();
        services.AddSingleton<PdfExportRenderer>();
        services.AddOptions<ExportOptions>().BindConfiguration("Exports").ValidateOnStart();
        services.AddSingleton<IValidateOptions<ExportOptions>, ExportOptionsValidator>();
        services.AddFeaturePolicy("artifacts");
        services.AddFeaturePolicy("feature:text", BuiltInAccess.ChatFeature, "artifacts");
    }

    public static void MapEndpoints(RouteGroupBuilder api) => ArtifactEndpoints.MapArtifacts(api);
}

internal sealed class ExportOptionsValidator : IValidateOptions<ExportOptions>
{
    public ValidateOptionsResult Validate(string? name, ExportOptions x)
        => x.BrowserChannel is "msedge" or "chrome" or "chromium" && x.TimeoutSeconds is >= 5 and <= 120
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid document export browser settings.");
}
