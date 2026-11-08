using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>暫停依排程執行；執行中的這一輪照常跑完，手動執行仍可用。會稽核前後值。</summary>
internal sealed class PauseScheduledTask(AppDbContext db, AdministrativeWrite write, ScheduledTasks tasks) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{key}/pause", (string key, ICurrentUser user, PauseScheduledTask handler, CancellationToken ct) => handler.HandleAsync(key, user.Id, ct))
        .RequireFeature(FeatureCatalog.SchedulingManage)
        .Audited(SchedulingAudit.TaskPaused)
        .WithName("PauseScheduledTask");

    public async Task<Result> HandleAsync(string key, Guid actor, CancellationToken ct)
    {
        var begun = await write.BeginAsync(ScheduledTaskRules.Target(db, key), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;
        if (await tasks.SetPausedAsync(key, paused: true, actor, ct) is { IsSuccess: false } failed) return failed.Error;
        return await transaction.CommitAsync(ct);
    }
}
