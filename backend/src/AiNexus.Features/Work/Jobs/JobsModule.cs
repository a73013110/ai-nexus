using AiNexus.Features.Work.Jobs.Management;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 持久化背景工作：其他模組以 <see cref="JobService"/> 排入工作，並實作 <see cref="IBackgroundJobHandler"/>；
/// worker 負責租約、執行、檢查點 fencing 與結果記錄。每個 use case 各一個檔案。
/// </summary>
public sealed class JobsModule : IFeatureModule
{
    // 端點註冊順序即 OpenAPI 發佈順序。
    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var jobs = api.MapGroup("/jobs");
        ListJobs.Map(jobs);
        GetJob.Map(jobs);
        CancelJob.Map(jobs);
        RetryJob.Map(jobs);
    }
}
