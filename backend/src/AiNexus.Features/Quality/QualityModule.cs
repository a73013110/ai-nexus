using AiNexus.Features.AccessControl;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Features.Jobs;
using AiNexus.Features.Quality.Feedback;
using AiNexus.Features.Quality.Evaluations;
using AiNexus.Features.Quality.RetrievalEvaluations;

namespace AiNexus.Features.Quality;

/// <summary>Answer feedback, model comparison sets and runs, and retrieval evaluations. Each use case has its own file.</summary>
public sealed class QualityModule : IFeatureModule
{
    /// <summary>Evaluation sets carry up to 224,000 characters of questions and references.</summary>
    public const int MaxSetCharacters = 224000;

    /// <summary>Applied to each /quality/sets endpoint; a nested route group would reorder the published OpenAPI document.</summary>
    internal static readonly long SetBodyLimit = RequestBodyLimits.ForJsonCharacters(MaxSetCharacters);

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<RetrievalEvaluationService>();
        services.AddScoped<IBackgroundJobHandler, RetrievalEvaluationHandler>();
        services.AddScoped<IBackgroundJobHandler, EvaluationHandler>();
        services.AddFeaturePolicy(FeatureIds.Quality);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        ReadFeedback.MapForMessage(api);
        SaveMessageFeedback.Map(api);
        var routes = api.MapGroup("/quality").RequireAuthorization(Policies.Quality).WithTags("Quality");
        ListRetrievalEvaluations.Map(routes);
        CreateRetrievalEvaluation.Map(routes);
        GetRetrievalReport.Map(routes);
        ReadFeedback.MapList(routes);
        BrowseEvaluationSets.MapList(routes);
        SaveEvaluationSet.MapCreate(routes);
        DeleteEvaluationSet.Map(routes);
        BrowseEvaluationSets.MapGet(routes);
        SaveEvaluationSet.MapUpdate(routes);
        ShareEvaluationSet.Map(routes);
        StartEvaluationRun.Map(routes);
        BrowseEvaluationRuns.Map(routes);
        ReviewEvaluationResult.Map(routes);
    }
}

internal sealed class QualityFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Quality, "品質評測", "/quality", 60));
