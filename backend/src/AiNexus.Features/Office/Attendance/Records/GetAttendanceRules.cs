using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Records;

/// <summary>
/// 出勤判定規則的說明（<see cref="AttendanceRules"/>），給出勤紀錄頁顯示：時間是目前的設定值，前端不寫死。
/// 與出勤紀錄同一個授權；規則全公司一套，不含個人資料，所以不套資料範圍。
/// </summary>
internal sealed class GetAttendanceRules(IOptions<AttendanceOptions> options)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("", (GetAttendanceRules handler) => TypedResults.Ok(handler.Handle()))
        .WithName("GetAttendanceRules");

    public AttendanceRulesDto Handle() => AttendanceRules.Describe(options.Value.Workday);
}
