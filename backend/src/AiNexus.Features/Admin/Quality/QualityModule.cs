using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Http;
using AiNexus.Platform.Modules;
using AiNexus.Features.Work.Jobs;
using AiNexus.Features.Admin.Quality.Feedback;
using AiNexus.Features.Admin.Quality.Evaluations;
using AiNexus.Features.Admin.Quality.RetrievalEvaluations;

namespace AiNexus.Features.Admin.Quality;

/// <summary>回答回饋、模型比較題組與執行，以及檢索評測。每個用例各自一個檔案。</summary>
public sealed class QualityModule : IFeatureModule
{
    /// <summary>題組的題目與參考答案合計上限（字元數）。</summary>
    public const int MaxSetCharacters = 224000;

    /// <summary>套用在每個 /quality/sets 端點上；若改用巢狀路由群組，會改變已發布的 OpenAPI 文件順序。</summary>
    internal static readonly long SetBodyLimit = RequestBodyLimits.ForJsonCharacters(MaxSetCharacters);

    public static void AddServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddBackgroundJobHandler<RetrievalEvaluationHandler>(RetrievalEvaluationHandler.JobKind, JobLane.Bulk);
        services.AddBackgroundJobHandler<EvaluationHandler>(EvaluationHandler.JobKind, JobLane.Bulk);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/messages").RequireFeature(FeatureCatalog.Chat)
            .MapSlice<ReadMessageFeedback>()
            .MapSlice<SaveMessageFeedback>()
            .MapSlice<DeleteMessageFeedback>();
        // 檢索評測是獨立頁面與授權（quality.retrieval），其餘題組、執行與回饋屬於品質評測頁。
        api.MapGroup("/quality").RequireFeature(FeatureCatalog.QualityRetrieval)
            .MapSlice<ListRetrievalEvaluations>()
            .MapSlice<CreateRetrievalEvaluation>()
            .MapSlice<GetRetrievalReport>();
        api.MapGroup("/quality").RequireFeature(FeatureCatalog.Quality)
            .MapSlice<ListFeedback>()
            .MapSlice<BrowseEvaluationSets>()
            .MapSlice<SaveEvaluationSet>()
            .MapSlice<DeleteEvaluationSet>()
            .MapSlice<ShareEvaluationSet>()
            .MapSlice<StartEvaluationRun>()
            .MapSlice<BrowseEvaluationRuns>()
            .MapSlice<ReviewEvaluationResult>();
    }
}
