using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Scheduling;
using AiNexus.Features.Office.CalendarSync.Sync;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Management;

/// <summary><see cref="CalendarId"/>：管理員在畫面上看到並確認的日曆；與本站設定不同時拒絕，設定在對話框開著時被換掉也不會清錯日曆。</summary>
public sealed record CalendarSyncResetRequest(string CalendarId);

internal sealed class CalendarSyncResetRequestValidator : RequestValidator<CalendarSyncResetRequest>
{
    public CalendarSyncResetRequestValidator() => RuleFor(x => x.CalendarId).RequiredText(200);
}

/// <summary>
/// 重設本站設定的日曆：刪除 Google 上本系統記錄的事件，再清掉這個日曆的事件列與同步紀錄（<see cref="CalendarSyncRunner.ResetAsync"/>）。
/// 交給同步任務執行，與排程中的同步共用租約，不會同時改同一個日曆；排進去就回 204，管理頁輪詢看結果。
/// 下一輪同步會依公文系統重新建立全部事件，所以這是「從頭同步一次」，不是停用；要讓日曆保持空的，先關掉 <c>CalendarSync:Enabled</c>。
/// </summary>
internal sealed class ResetCalendarSync(ScheduledTasks tasks, AuditTrail audit, IOptions<CalendarSyncOptions> options) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/reset", (CalendarSyncResetRequest body, ICurrentUser user, ResetCalendarSync handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct))
        .RequireFeature(FeatureCatalog.CalendarSyncManage)
        .Audited(CalendarSyncAudit.ResetRequested)
        .WithName("ResetCalendarSync");

    public async Task<Result> HandleAsync(Guid actor, CalendarSyncResetRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        audit.Describe(details: new { calendarId = settings.CalendarId });
        if (!settings.Enabled) return CalendarSyncErrors.Disabled;
        if (!string.Equals(request.CalendarId.Trim(), settings.CalendarId, StringComparison.Ordinal)) return CalendarSyncErrors.ResetCalendarMismatch;
        return await tasks.RequestRunAsync(CalendarSyncTask.Definition.Key, actor, new CalendarSyncParameters(ConfirmRemovals: false, Reset: true), ct);
    }
}
