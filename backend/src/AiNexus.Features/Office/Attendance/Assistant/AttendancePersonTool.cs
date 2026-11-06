using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>
/// 某人在出勤名單上的設定：帳號狀態、每段持卡期間與卡是否隱藏、已知的設定問題，以及名單怎麼組成的〈規則〉。
/// 回答「為什麼還在、為什麼不見、為什麼離職了還有紀錄」：原因組合很多（沒結束的持有、隱藏的卡、沒綁帳號、換卡空窗、調單位），
/// 所以給完整設定與條文讓模型說明，只有已知的問題才寫成〈檢查〉。資料範圍與出勤紀錄相同（<see cref="AttendanceSetupReader"/>）。
/// </summary>
internal sealed class AttendancePersonTool(AttendanceSetupReader setups, TimeProvider clock) : IAssistantTool
{
    public string Name => "attendance_person";
    public string Title => "查詢名單設定";
    public string Description => "查某人為什麼會或不會出現在出勤紀錄：帳號是否停用、持有哪些卡與期間、卡是否在出勤紀錄隱藏、設定上的問題與名單規則。person 要填。";
    public string Feature => FeatureCatalog.AttendanceRecords;
    public string Guidance => AttendanceAssistant.Guidance;
    public AuditAction? Audit => AttendanceAudit.AssistantRead;

    public AssistantParameters Parameters { get; } = new AssistantParameters()
        .Text("person", "帳號、姓名或卡號（完全相同才算）");

    public async Task<Result<AssistantToolResult>> InvokeAsync(AssistantToolCall call, CancellationToken ct)
    {
        if (call.Arguments.Text("person") is not { } person) return AttendanceErrors.RecordsFilterInvalid;
        var people = await setups.FindAsync(call.Viewer, person, ct);
        return AttendanceAssistant.Setup(person, people, clock.GetUtcNow());
    }
}
