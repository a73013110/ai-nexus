using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Configuration;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>Read-only Gitea access with each user's own token, file imports into knowledge, and background code reviews. Each use case has its own file.</summary>
public sealed class RepositoriesModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<GiteaOptions, GiteaOptionsValidator>(GiteaOptions.Section);
        services.AddScoped<IGiteaClient, GiteaClient>();
        services.AddScoped<RepositoryService>();
        services.AddScoped<RepositoryReviewService>();
        services.AddScoped<IBackgroundJobHandler, RepositoryReviewHandler>();
        services.AddSingleton<RepositoryWriteLock>();
        services.AddFeaturePolicy(FeatureIds.Repositories);
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/repositories").RequireAuthorization(Policies.Repositories).WithTags("Repositories");
        BrowseRepositories.MapCommits(routes);
        ListRepositoryReviews.Map(routes);
        CreateRepositoryReview.Map(routes);
        ReadRepositoryReview.Map(routes);
        ChangeRepositoryReviewJob.Map(routes);
        GetRepositoryConnection.Map(routes);
        ConnectRepository.Map(routes);
        DisconnectRepository.Map(routes);
        BrowseRepositories.Map(routes);
        ImportRepositoryFile.Map(routes);
    }
}

internal sealed class RepositoriesFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Repositories, "程式庫", "/repositories", 65));
