using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>恢復依排程執行；暫停期間錯過的排程立即補跑一次（不逐次回補）。會稽核前後值。</summary>
internal sealed class ResumeScheduledTask(AppDbContext db, AdministrativeWrite write, ScheduledTasks tasks) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{key}/resume", (string key, ICurrentUser user, ResumeScheduledTask handler, CancellationToken ct) => handler.HandleAsync(key, user.Id, ct))
        .RequireFeature(FeatureCatalog.SchedulingManage)
        .Audited(SchedulingAudit.TaskResumed)
        .WithName("ResumeScheduledTask");

    public async Task<Result> HandleAsync(string key, Guid actor, CancellationToken ct)
    {
        var begun = await write.BeginAsync(ScheduledTaskRules.Target(db, key), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;
        if (await tasks.SetPausedAsync(key, paused: false, actor, ct) is { IsSuccess: false } failed) return failed.Error;
        return await transaction.CommitAsync(ct);
    }
}
