using AiNexus.Features.AccessControl;
using AiNexus.Features.Operations;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Quality;

public sealed class QualityModule : IFeatureModule
{
    /// <summary>Evaluation sets carry up to 224,000 characters of questions and references.</summary>
    public const int MaxSetCharacters = 224000;

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<QualityService>();
        services.AddScoped<RetrievalEvaluationService>();
        services.AddScoped<IBackgroundJobHandler, RetrievalEvaluationHandler>();
        services.AddScoped<IBackgroundJobHandler, EvaluationHandler>();
        services.AddFeaturePolicy(FeatureIds.Quality);
    }

    public static void MapEndpoints(RouteGroupBuilder api) => QualityEndpoints.MapQuality(api);
}
