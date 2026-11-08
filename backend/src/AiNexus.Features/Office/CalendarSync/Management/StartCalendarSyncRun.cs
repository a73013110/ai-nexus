using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Office.CalendarSync.Sync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary><see cref="ConfirmRemovals"/>：確認執行被安全閥擋下的移除（管理頁看過待移除的數量後才送 true）。</summary>
[ValidatedInHandler("The body is a single boolean; every value is valid.")]
public sealed record CalendarSyncRunRequest(bool ConfirmRemovals);

/// <summary>
/// 立即同步一輪：排進排程任務後就回 204，不在請求內等 Google（第一次同步可能有數百筆）；管理頁輪詢狀態看結果。正在同步時排在這一輪之後。
/// 手動同步不等失敗事件的退避時間，修好設定或權限後按一次就全部補上。未啟用時拒絕：只在正式站啟用，避免開發機改到正式日曆。
/// </summary>
internal sealed class StartCalendarSyncRun(ScheduledTasks tasks, AuditTrail audit, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/runs", (CalendarSyncRunRequest body, ICurrentUser user, StartCalendarSyncRun handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct))
        .RequireFeature(FeatureCatalog.CalendarSyncManage)
        .Audited(CalendarSyncAudit.RunRequested)
        .WithName("StartCalendarSyncRun");

    public async Task<Result> HandleAsync(Guid actor, CalendarSyncRunRequest request, CancellationToken ct)
    {
        audit.Describe(details: new { confirmRemovals = request.ConfirmRemovals });
        if (!options.Value.Enabled) return CalendarSyncErrors.Disabled;
        return await tasks.RequestRunAsync(CalendarSyncTask.Definition.Key, actor, new CalendarSyncParameters(request.ConfirmRemovals), ct);
    }
}
