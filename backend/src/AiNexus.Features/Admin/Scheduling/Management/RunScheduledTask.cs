using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>
/// 立即執行一輪：記下要求就回 204，不在請求內等任務跑完；頁面輪詢看結果。正在執行時排在這一輪結束後，暫停中也可以手動執行。
/// </summary>
internal sealed class RunScheduledTask(ScheduledTasks tasks) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{key}/run", (string key, ICurrentUser user, RunScheduledTask handler, CancellationToken ct) => handler.HandleAsync(key, user.Id, ct))
        .RequireFeature(FeatureCatalog.SchedulingManage)
        .Audited(SchedulingAudit.RunRequested)
        .WithName("RunScheduledTask");

    public Task<Result> HandleAsync(string key, Guid actor, CancellationToken ct) => tasks.RequestRunAsync(key, actor, null, ct);
}
