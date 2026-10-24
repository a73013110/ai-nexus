using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Integrations.Gd;
using AiNexus.Features.Staff.Attendance.Calendar;
using AiNexus.Features.Staff.Attendance.Cards;
using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Features.Staff.Attendance.Imports;
using AiNexus.Features.Staff.Attendance.Leave;
using AiNexus.Features.Staff.Attendance.Records;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Accounts;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Staff.Attendance;

/// <summary>
/// 出勤打卡（ADR 0018）：出勤紀錄（依資料範圍查詢、查詢時判定，可匯出 Excel；規則說明由 <see cref="AttendanceRules"/> 產生）、卡號對應（卡片、持有期間與未歸戶卡號）、刷卡匯入與假日設定。
/// 卡號對應的寫入共用 AccessControl 的 <see cref="AdministrativeAudit"/>：同一個交易、行政寫入鎖與稽核，並發的重疊檢查靠它串行化。
/// 假單與出差單由 <see cref="LeaveSheetReader"/> 經 Integrations 的 <see cref="GdAccess"/> 唯讀查詢公文系統（GD），連不上時降級而不拋例外；GD 的可用性接到即時監控（<c>gd</c>）。
/// 假日設定經 <see cref="GdAccess"/> 查詢，經 <see cref="GdCalendarWriter"/> 回寫，兩者用同一個 <c>Databases:Gd</c>；設為 <c>ReadOnly=true</c> 時只停用回寫。
/// 刷卡匯入由 <see cref="PunchImportWorker"/> 定期掃描打卡機輸出資料夾，HR 也可以手動指定日期區間重匯；匯入狀態接到即時監控（<c>attendance-import</c>）。
/// </summary>
public sealed class AttendanceModule : IFeatureModule
{
    /// <summary>出勤紀錄匯出：每次都查 GD 並產生整段的試算表，每人每分鐘限 5 次。</summary>
    public const string ExportRateLimit = "attendance-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(ExportRateLimit, 5));
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceRecords);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCards);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCardsMaintain);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceImports);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceImportsReimport);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCalendar);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCalendarMaintain);
        builder.Services.AddSettings<AttendanceOptions, AttendanceOptionsValidator>(AttendanceOptions.Section);
        builder.Services.AddSingleton<PunchImportGate>();
        builder.Services.AddSingleton<PunchImportState>();
        builder.Services.AddScoped<PunchImporter>();
        builder.Services.AddHostedService<PunchImportWorker>();
        builder.Services.AddMonitoringSource<PunchImportMonitoring>();
        builder.Services.AddAccountReviewSource<CardAccountReview>();
        // GD 的查詢、帳號對應與監控由 Integrations 註冊（GdAccess、GdAccounts）；出勤只註冊自己的讀取與回寫。
        builder.Services.AddScoped<LeaveSheetReader>();
        builder.Services.AddSingleton<CalendarReader>();
        builder.Services.AddSingleton<GdCalendarWriter>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        var records = api.MapGroup("/attendance-records").RequireAuthorization(Policies.AttendanceRecords);
        ListAttendanceRecords.Map(records);
        ExportAttendanceRecords.Map(records);
        GetAttendanceRules.Map(api.MapGroup("/attendance-rules").RequireAuthorization(Policies.AttendanceRecords));

        var cards = api.MapGroup("/attendance-cards").RequireAuthorization(Policies.AttendanceCards);
        ListCards.Map(cards);
        CreateCard.Map(cards);
        UpdateCard.Map(cards);
        DeleteCard.Map(cards);

        var assignments = api.MapGroup("/attendance-card-assignments").RequireAuthorization(Policies.AttendanceCards);
        ListCardAssignments.Map(assignments);
        CreateCardAssignment.Map(assignments);
        UpdateCardAssignment.Map(assignments);
        DeleteCardAssignment.Map(assignments);

        ListUnattributedCards.Map(api.MapGroup("/attendance-unattributed-cards").RequireAuthorization(Policies.AttendanceCards));

        var imports = api.MapGroup("/attendance-imports").RequireAuthorization(Policies.AttendanceImports);
        ListPunchImports.Map(imports);
        ReimportPunchFiles.Map(imports);

        var calendar = api.MapGroup("/attendance-calendar-days").RequireAuthorization(Policies.AttendanceCalendar);
        ListCalendarDays.Map(calendar);
        UpdateCalendarDay.Map(calendar);
    }
}
