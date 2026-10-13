using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Features.Jobs;
using AiNexus.Features.Repositories.Browsing;
using AiNexus.Features.Repositories.Connections;
using AiNexus.Features.Repositories.Handoff;
using AiNexus.Features.Repositories.Reviews;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Repositories;

/// <summary>
/// Gitea with each user's own token: browsing code, history, issues and pull requests; handing files to a conversation or a
/// knowledge collection; background AI reviews, which may be posted back as a pull-request comment.
/// </summary>
public sealed class RepositoriesModule : IFeatureModule
{
    public const string ReadRateLimit = "repositories-read";
    public const string WriteRateLimit = "repositories-write";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddDashboardSource<RepositoriesDashboard>();
        services.AddSettings<GiteaOptions, GiteaOptionsValidator>(GiteaOptions.Section);
        services.AddScoped<IGiteaClient, GiteaClient>();
        services.AddScoped<RepositoryService>();
        services.AddScoped<RepositoryReviewService>();
        services.AddScoped<IBackgroundJobHandler, RepositoryReviewHandler>();
        services.AddScoped<IBackgroundJobHandler, RepositoryImportHandler>();
        services.AddSingleton<RepositoryWriteLock>();
        services.AddFeaturePolicy(FeatureIds.Repositories);
        // Every read is a Gitea call made with the user's token; writes also start model work, uploads or remote comments.
        services.AddRateLimiter(options => options.AddPerUserLimit(ReadRateLimit, 240).AddPerUserLimit(WriteRateLimit, 20));
    }

    // Endpoint order is the published OpenAPI order.
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/repositories").RequireAuthorization(Policies.Repositories).WithTags("Repositories").RequireRateLimiting(ReadRateLimit);
        GetRepositoryConnection.Map(routes);
        ConnectRepository.Map(routes);
        DisconnectRepository.Map(routes);
        BrowseRepositories.MapList(routes);
        ReadRepositoryReview.Map(routes);
        ChangeRepositoryReviewJob.Map(routes);
        PostRepositoryReviewComment.Map(routes);

        var repository = routes.MapGroup("/{owner}/{name}");
        BrowseRepositories.Map(repository);
        PinRepository.Map(repository);
        UnpinRepository.Map(repository);
        BrowseRepositoryHistory.Map(repository);
        BrowseRepositoryIssues.Map(repository);
        StartRepositoryChat.Map(repository);
        ImportRepositoryFolder.Map(repository);
        ListRepositoryReviews.Map(repository);
        CreateRepositoryReview.Map(repository);
    }
}

internal sealed class RepositoriesFeatures() : FeatureSeed(new PlatformFeature(FeatureIds.Repositories, "程式庫", "/repositories", 60));
