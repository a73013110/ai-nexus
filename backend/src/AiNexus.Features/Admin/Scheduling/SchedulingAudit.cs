using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Scheduling;

/// <summary>
/// 排程任務的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。自動執行不稽核，執行紀錄就是證據。
/// 暫停、恢復用既有的 disabled／enabled 動詞：停的是依排程執行，手動執行仍可用。
/// </summary>
public static class SchedulingAudit
{
    public static readonly AuditAction RunRead = AuditAction.Access("scheduling.run.read", "查詢排程任務的執行紀錄");
    public static readonly AuditAction RunRequested = AuditAction.Administration("scheduling.run.requested", "手動執行排程任務");
    public static readonly AuditAction RunCancelled = AuditAction.Administration("scheduling.run.cancelled", "停止執行中的排程任務");
    public static readonly AuditAction TaskPaused = AuditAction.Administration("scheduling.task.disabled", "暫停排程任務");
    public static readonly AuditAction TaskResumed = AuditAction.Administration("scheduling.task.enabled", "恢復排程任務");
}
