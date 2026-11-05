using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance.Records;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>
/// 一個人一天的判定過程（與出勤紀錄點開狀態看到的同一份 <see cref="AttendanceExplanation"/>）。
/// 和 <see cref="ExplainAttendanceDay"/> 一樣以 <see cref="ListAttendanceRecords"/> 查那一天，資料範圍外的人查不到；人由帳號、姓名或卡號指定，同名時要用帳號。
/// </summary>
internal sealed class AttendanceDayTool(ListAttendanceRecords records, IOptions<AttendanceOptions> options) : IAssistantTool
{
    public string Name => "attendance_day_explanation";
    public string Title => "查詢判定過程";
    public string Description => "查某人某一天的出勤為什麼是這個結果：刷卡、應下班、表單抵扣的每一步與依據的規則。date 與 person 都要填。";
    public string Feature => FeatureCatalog.AttendanceRecords;
    public string Guidance => AttendanceAssistant.Guidance;
    public AuditAction? Audit => AttendanceAudit.AssistantRead;

    public AssistantParameters Parameters { get; } = new AssistantParameters()
        .Date("date", "出勤日，yyyy-MM-dd")
        .Text("person", "帳號、姓名或卡號；同名時用帳號");

    public async Task<Result<AssistantToolResult>> InvokeAsync(AssistantToolCall call, CancellationToken ct)
    {
        if (call.Arguments.Date("date") is not { } date || call.Arguments.Text("person") is not { } person) return AttendanceErrors.RecordsFilterInvalid;

        var filter = new AttendanceRecordsFilter { From = date, To = date, IncludeDaysOff = true };
        var read = await records.HandleAsync(call.Viewer, filter, ListAttendanceRecords.Page.All, ct);
        if (!read.IsSuccess) return read.Error;

        var found = read.Value.People.Where(x => AttendanceAssistant.Matches(x, person))
            .SelectMany(x => x.Days.Where(day => day.Date == date).Select(day => (Person: x, Day: day)))
            .Take(2).ToList();
        if (found is not [var (match, day)]) return AttendanceErrors.RecordNotFound;

        var explanation = AttendanceExplanation.Explain(match, day, options.Value.Workday, read.Value.Today);
        return AttendanceAssistant.Explanation(match, explanation, options.Value.Workday);
    }
}
