using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Repositories.Browsing;
using AiNexus.Features.Work.Repositories.Connections;
using AiNexus.Features.Work.Repositories.Handoff;
using AiNexus.Features.Work.Repositories.Reviews;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Repositories;

/// <summary>
/// 以每位使用者自己的 token 存取 Gitea：瀏覽程式碼、歷史、issue 與 pull request；把檔案交給對話或知識庫；
/// 背景 AI review，結果可選擇回貼成 pull request 留言。
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
        services.AddFeaturePolicy(FeatureCatalog.Repositories);
        // 每次讀取都會實際呼叫 Gitea（使用使用者的 token）；寫入還可能啟動模型工作、上傳檔案或遠端留言，所以限制較嚴。
        services.AddRateLimiter(options => options.AddPerUserLimit(ReadRateLimit, 240).AddPerUserLimit(WriteRateLimit, 20));
    }

    // 端點註冊順序即為發布的 OpenAPI 順序；調整時需同步重產 openapi.json。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/repositories").RequireAuthorization(Policies.Repositories).RequireRateLimiting(ReadRateLimit);
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
