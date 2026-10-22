using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Staff.Attendance.Cards;
using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Features.Staff.Attendance.Imports;
using AiNexus.Features.Staff.Attendance.Leave;
using AiNexus.Platform.Configuration;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Staff.Attendance;

/// <summary>
/// 出勤打卡（ADR 0018）。目前有卡號對應（卡片、持有期間與未歸戶卡號）與刷卡匯入；出勤紀錄與假日設定隨後加入。
/// 卡號對應的寫入共用 AccessControl 的 <see cref="AdministrativeAudit"/>：同一個交易、行政寫入鎖與稽核，並發的重疊檢查靠它串行化。
/// 假單與出差單由 <see cref="LeaveSheetReader"/> 經 <see cref="GdAccess"/> 唯讀查詢公文系統（GD），連不上時降級而不拋例外。
/// 刷卡匯入由 <see cref="PunchImportWorker"/> 定期掃描打卡機輸出資料夾，HR 也可以手動指定日期重匯；匯入狀態接到即時監控（<c>attendance-import</c>）。
/// </summary>
public sealed class AttendanceModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCards);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCardsMaintain);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceImports);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceImportsReimport);
        builder.Services.AddSettings<AttendanceOptions, AttendanceOptionsValidator>(AttendanceOptions.Section);
        builder.Services.AddSingleton<PunchImportGate>();
        builder.Services.AddSingleton<PunchImportState>();
        builder.Services.AddScoped<PunchImporter>();
        builder.Services.AddHostedService<PunchImportWorker>();
        builder.Services.AddMonitoringSource<AttendanceMonitoring>();
        // 和 Integrations 共用同一個唯讀 GD 連線；用到的資料庫各模組自己宣告，不靠別的模組代為註冊（重複宣告是冪等的）。
        builder.Services.AddSqlDatabase<GdDatabase>();
        builder.Services.AddSingleton<GdAccess>();
        builder.Services.AddScoped<GdAccounts>();
        builder.Services.AddScoped<LeaveSheetReader>();
    }

    public static void MapEndpoints(RouteGroupBuilder api)
    {
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
        ReimportPunchFile.Map(imports);
    }
}
