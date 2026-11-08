using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>
/// 停止執行中的這一輪並撤回排隊中的手動要求。停止是合作式的：本機執行立即取消，其他程序在下一次續租（數秒內）讀到；
/// 任務分批提交，已完成的部分保留，下次執行從資料狀態接續。沒有在執行時是無作用的成功。
/// </summary>
internal sealed class CancelScheduledTask(ScheduledTasks tasks) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{key}/cancel", (string key, CancelScheduledTask handler, CancellationToken ct) => handler.HandleAsync(key, ct))
        .RequireFeature(FeatureCatalog.SchedulingManage)
        .Audited(SchedulingAudit.RunCancelled)
        .WithName("CancelScheduledTask");

    public Task<Result> HandleAsync(string key, CancellationToken ct) => tasks.CancelAsync(key, ct);
}
