using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Scheduling.Engine;
using AiNexus.Features.Admin.Scheduling.Management;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Scheduling;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>
/// 排程任務的統一管理：各模組實作 Platform 的 <c>IScheduledTask</c>，這裡的引擎（<see cref="ScheduledTaskHost"/>）負責排程、跨程序租約、
/// 逾時與停止、執行紀錄與連續失敗的通知；「背景作業」頁看下次執行、執行中與紀錄，可立即執行、停止、暫停。
/// 同一頁另外列出常駐服務（<c>IResidentService</c>）在這個程序的狀態。
/// 狀態存在 <c>scheduling.ScheduledTasks</c>（每任務一列）與 <c>scheduling.ScheduledTaskRuns</c>。
/// </summary>
public sealed class SchedulingModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder) =>
        builder.Services.AddSettings<SchedulingOptions, SchedulingOptionsValidator>(SchedulingOptions.Section);

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/scheduled-tasks").RequireFeature(FeatureCatalog.Scheduling)
            .MapSlice<ListScheduledTasks>()
            .MapSlice<ListScheduledTaskRuns>()
            .MapSlice<RunScheduledTask>()
            .MapSlice<CancelScheduledTask>()
            .MapSlice<PauseScheduledTask>()
            .MapSlice<ResumeScheduledTask>();
        api.MapGroup("/resident-services").RequireFeature(FeatureCatalog.Scheduling)
            .MapSlice<ListResidentServices>();
    }

    /// <summary>任務 Key 重複、格式不符，<c>Scheduling:Tasks</c> 缺漏、寫錯任務名稱，或常駐服務的 Key 重複時啟動失敗。</summary>
    public static void VerifyStartup(IServiceProvider services)
    {
        _ = services.GetRequiredService<ScheduledTaskCatalog>();
        _ = services.GetRequiredService<ResidentServices>();
    }
}
