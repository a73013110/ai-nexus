using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Jobs;

/// <summary>背景任務的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class JobsAudit
{
    public static readonly AuditAction JobRead = AuditAction.Access("jobs.job.read", "查詢背景任務");
    public static readonly AuditAction JobCancelled = AuditAction.Activity("jobs.job.cancelled", "停止背景任務");
    public static readonly AuditAction JobRetried = AuditAction.Activity("jobs.job.retried", "重試背景任務");
}
