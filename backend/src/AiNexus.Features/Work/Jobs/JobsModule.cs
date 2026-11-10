using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Jobs.Management;
using AiNexus.Features.Work.Jobs.Queue;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 持久化背景工作：其他模組以 <see cref="JobService"/> 排入工作，並實作 <see cref="IBackgroundJobHandler"/>；
/// worker 負責租約、執行、檢查點 fencing 與結果記錄。每個 use case 各一個檔案。
/// 所有人的佇列（<c>/background-jobs</c>）屬於「背景作業」頁，權限跟著該頁；使用者自己的工作在 <c>/jobs</c>。
/// </summary>
public sealed class JobsModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
        => builder.Services.AddSettings<JobsOptions, JobsOptionsValidator>(JobsOptions.Section);

    // 端點註冊順序即 OpenAPI 發佈順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/jobs")
            .MapSlice<ListJobs>()
            .MapSlice<GetJob>()
            .MapSlice<CancelJob>()
            .MapSlice<RetryJob>();
        api.MapGroup("/background-jobs").RequireFeature(FeatureCatalog.Scheduling)
            .MapSlice<ListQueuedJobs>()
            .MapSlice<CancelQueuedJob>();
    }
}
