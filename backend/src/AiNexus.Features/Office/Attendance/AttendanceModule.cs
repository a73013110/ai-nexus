using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Office.Attendance.Calendar;
using AiNexus.Features.Office.Attendance.Cards;
using AiNexus.Features.Office.Attendance.Gd;
using AiNexus.Features.Office.Attendance.Imports;
using AiNexus.Features.Office.Attendance.Forms;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.Attendance;

/// <summary>
/// 出勤打卡（ADR 0018）：出勤紀錄（依資料範圍查詢、查詢時判定，可匯出 Excel；規則說明由 <see cref="AttendanceRules"/> 產生）、卡號對應（卡片、持有期間與在出勤紀錄隱藏）、刷卡匯入與假日設定。
/// 卡號對應的寫入共用 AccessControl 的 <see cref="AdministrativeWrite"/>：同一個交易與稽核，並發的重疊檢查靠它取的具名鎖排隊；刷卡匯入寫入時也取同一把鎖。
/// 假單與出差單由 <see cref="GdFormReader"/> 經 Gd 模組的 <see cref="GdAccounts"/> 與 <c>ExternalDatabase&lt;GdDatabase&gt;</c> 唯讀查詢公文系統（GD），連不上時降級而不拋例外；GD 的可用性接到即時監控（<c>gd</c>）。
/// 單張表單的內容（<see cref="GetGdForm"/>）另用 Gd 模組的流程與附件讀取，附件經 RemoteFiles 的短期憑證下載。
/// 假日設定經同一個 <c>ExternalDatabase&lt;GdDatabase&gt;</c> 查詢，經 <see cref="GdCalendarWriter"/> 回寫，兩者用同一個 <c>Databases:Gd</c>；設為 <c>ReadOnly=true</c> 時只停用回寫。
/// 刷卡匯入由 <see cref="PunchImportWorker"/> 定期掃描打卡機輸出資料夾，HR 也可以手動指定日期區間重匯；匯入狀態接到即時監控（<c>attendance-import</c>）。
/// </summary>
public sealed class AttendanceModule : IFeatureModule
{
    /// <summary>出勤紀錄匯出：每次都查 GD 並產生整段的試算表，每人每分鐘限 5 次。</summary>
    public const string ExportRateLimit = "attendance-export";

    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options => options.AddPerUserLimit(ExportRateLimit, 5));
        builder.Services.AddSettings<AttendanceOptions, AttendanceOptionsValidator>(AttendanceOptions.Section);
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
        api.MapGroup("/attendance-records").RequireFeature(FeatureCatalog.AttendanceRecords)
            .MapSlice<ListAttendanceRecords>()
            .MapSlice<ListAttendancePeople>()
            .MapSlice<ExportAttendanceRecords>();
        api.MapGroup("/attendance-rules").RequireFeature(FeatureCatalog.AttendanceRecords)
            .MapSlice<GetAttendanceRules>();
        api.MapGroup("/attendance-forms").RequireFeature(FeatureCatalog.AttendanceRecords)
            .MapSlice<GetGdForm>();

        api.MapGroup("/attendance-cards").RequireFeature(FeatureCatalog.AttendanceCards)
            .MapSlice<ListCards>()
            .MapSlice<CreateCard>()
            .MapSlice<UpdateCard>()
            .MapSlice<DeleteCard>()
            .MapSlice<ListCardPunches>();

        api.MapGroup("/attendance-card-assignments").RequireFeature(FeatureCatalog.AttendanceCards)
            .MapSlice<ListCardAssignments>()
            .MapSlice<CreateCardAssignment>()
            .MapSlice<UpdateCardAssignment>()
            .MapSlice<DeleteCardAssignment>();

        api.MapGroup("/attendance-imports").RequireFeature(FeatureCatalog.AttendanceImports)
            .MapSlice<ListPunchImports>()
            .MapSlice<ListPunchImportPunches>()
            .MapSlice<ReimportPunchFiles>();

        api.MapGroup("/attendance-calendar-days").RequireFeature(FeatureCatalog.AttendanceCalendar)
            .MapSlice<ListCalendarDays>()
            .MapSlice<UpdateCalendarDay>();
    }
}
