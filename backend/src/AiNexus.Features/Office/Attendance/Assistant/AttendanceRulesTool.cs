using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Assistant;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.Attendance.Assistant;

/// <summary>出勤判定規則（與出勤紀錄的「判定規則」同一份條文）：不含個人資料，不寫稽核。</summary>
internal sealed class AttendanceRulesTool(IOptions<AttendanceOptions> options) : IAssistantTool
{
    public string Name => "attendance_rules";
    public string Title => "查詢判定規則";
    public string Description => "查出勤的判定規則：上下班時間、彈性上班、遲到早退、補足與表單怎麼抵。";
    public string Feature => FeatureCatalog.AttendanceRecords;
    public string Guidance => AttendanceAssistant.Guidance;
    public AuditAction? Audit => null;
    public AssistantParameters Parameters { get; } = AssistantParameters.None;

    public Task<Result<AssistantToolResult>> InvokeAsync(AssistantToolCall call, CancellationToken ct)
        => Task.FromResult<Result<AssistantToolResult>>(new AssistantToolResult("判定規則", [.. AttendanceAssistant.Rules(options.Value.Workday)]));
}
