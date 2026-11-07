using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
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
/// 立即同步一輪：排進背景工作後就回 204，不在請求內等 Google（第一次同步可能有數百筆）；管理頁輪詢狀態看結果。
/// 手動同步不等失敗事件的退避時間，修好設定或權限後按一次就全部補上。未啟用時拒絕：只在正式站啟用，避免開發機改到正式日曆。
/// </summary>
internal sealed class StartCalendarSyncRun(CalendarSyncState state, AuditTrail audit, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/runs", (CalendarSyncRunRequest body, ICurrentUser user, StartCalendarSyncRun handler) => handler.Handle(user.Id, body))
        .RequireFeature(FeatureCatalog.CalendarSyncManage)
        .Audited(CalendarSyncAudit.RunRequested)
        .WithName("StartCalendarSyncRun");

    public Result Handle(Guid actor, CalendarSyncRunRequest request)
    {
        audit.Describe(details: new { confirmRemovals = request.ConfirmRemovals });
        if (!options.Value.Enabled) return CalendarSyncErrors.Disabled;
        state.Request(new(actor, request.ConfirmRemovals));
        return Result.Success;
    }
}
