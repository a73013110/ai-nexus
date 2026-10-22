using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Staff.Attendance.Cards;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Staff.Attendance;

/// <summary>
/// 出勤打卡（ADR 0018）。目前只有卡號對應：卡片、持有期間與未歸戶卡號；刷卡匯入、出勤紀錄與假日設定隨後加入。
/// 卡號對應的寫入共用 AccessControl 的 <see cref="AdministrativeAudit"/>：同一個交易、行政寫入鎖與稽核，並發的重疊檢查靠它串行化。
/// </summary>
public sealed class AttendanceModule : IFeatureModule
{
    public static void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCards);
        builder.Services.AddFeaturePolicy(FeatureCatalog.AttendanceCardsMaintain);
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
    }
}
