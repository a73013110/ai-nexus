using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Flow;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Work.Repositories.Access;
using AiNexus.Features.Work.Repositories.Browsing;
using AiNexus.Features.Work.Repositories.Connections;
using AiNexus.Features.Work.Repositories.Handoff;
using AiNexus.Features.Work.Repositories.Reviews;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Repositories;

/// <summary>
/// 以每位使用者自己的 token 存取 Gitea：瀏覽程式碼、歷史、issue 與 pull request；把檔案交給對話或知識庫；
/// 背景 AI review，結果可選擇回貼成 pull request 留言。
/// 儲存庫權限（ADR 0037）另以服務帳號管理組織儲存庫的協作者：申請走簽核，負責人與管理員可直接授予，同步任務收斂到 Gitea。
/// </summary>
public sealed class RepositoriesModule : IFeatureModule
{
    public const string ReadRateLimit = "repositories-read";
    public const string WriteRateLimit = "repositories-write";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSettings<GiteaOptions, GiteaOptionsValidator>(GiteaOptions.Section);
        services.AddBackgroundJobHandler<RepositoryReviewHandler>(RepositoryReviewHandler.JobKind);
        services.AddBackgroundJobHandler<RepositoryImportHandler>(RepositoryImportHandler.JobKind);
        services.AddFlowKind<RepositoryAccessFlow>(RepositoryAccessFlow.Definition)
            .AddApproverRule<RepositoryStewardsRule>(RepositoryStewardsRule.Definition);
        // 每次讀取都會實際呼叫 Gitea（使用使用者的 token）；寫入還可能啟動模型工作、上傳檔案或遠端留言，所以限制較嚴。
        services.AddRateLimiter(options => options.AddPerUserLimit(ReadRateLimit, 240).AddPerUserLimit(WriteRateLimit, 20));
    }

    // 端點註冊順序即為發布的 OpenAPI 順序；調整時需同步重產 openapi.json。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/repositories").RequireFeature(FeatureCatalog.Repositories).RequireRateLimiting(ReadRateLimit);
        routes.MapSlice<GetRepositoryConnection>()
            .MapSlice<ConnectRepository>()
            .MapSlice<DisconnectRepository>()
            .MapSlice<BrowseRepositories>()
            .MapSlice<ReadRepositoryReview>()
            .MapSlice<ChangeRepositoryReviewJob>()
            .MapSlice<PostRepositoryReviewComment>();

        routes.MapGroup("/{owner}/{name}")
            .MapSlice<PinRepository>()
            .MapSlice<UnpinRepository>()
            .MapSlice<BrowseRepositoryHistory>()
            .MapSlice<BrowseRepositoryIssues>()
            .MapSlice<StartRepositoryChat>()
            .MapSlice<ImportRepositoryFolder>()
            .MapSlice<ListRepositoryReviews>()
            .MapSlice<CreateRepositoryReview>();

        api.MapGroup("/managed-repositories").RequireFeature(FeatureCatalog.RepositoriesAccess)
            .MapSlice<ListManagedRepositories>()
            .MapSlice<GetManagedRepository>()
            .MapSlice<RequestRepositoryAccess>()
            .MapSlice<GrantRepositoryAccess>()
            .MapSlice<RevokeRepositoryGrant>()
            .MapSlice<SaveManagedRepositorySettings>()
            .MapSlice<SyncManagedRepositories>();
    }
}
