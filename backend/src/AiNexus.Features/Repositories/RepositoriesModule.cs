using AiNexus.Features.AccessControl;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Repositories;

/// <summary>Read-only Gitea access with each user's own token, file imports into knowledge, and background code reviews. Each use case has its own file.</summary>
public sealed class RepositoriesModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<GiteaOptions>().BindConfiguration("Integrations:Connectors:Gitea").ValidateOnStart();
        services.AddSingleton<IValidateOptions<GiteaOptions>, GiteaOptionsValidator>();
        services.AddScoped<IGiteaClient, GiteaClient>();
        services.AddScoped<RepositoryService>();
        services.AddScoped<RepositoryReviewService>();
        services.AddScoped<ListRepositoryReviews>();
        services.AddScoped<CreateRepositoryReview>();
        services.AddScoped<ReadRepositoryReview>();
        services.AddScoped<ConnectRepository>();
        services.AddScoped<ImportRepositoryFile>();
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

internal sealed class GiteaOptionsValidator : IValidateOptions<GiteaOptions>
{
    public ValidateOptionsResult Validate(string? name, GiteaOptions x)
        => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
           && x.TimeoutSeconds is >= 2 and <= 30 && x.MaxFileBytes is >= 1024 and <= 500000
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid Gitea connector settings.");
}
