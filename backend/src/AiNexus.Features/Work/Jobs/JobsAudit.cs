using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Jobs;

/// <summary>背景任務的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class JobsAudit
{
    public static readonly AuditAction JobRead = AuditAction.Access("jobs.job.read", "查詢背景任務");
    public static readonly AuditAction JobCancelled = AuditAction.Activity("jobs.job.cancelled", "停止背景任務");
    public static readonly AuditAction JobRetried = AuditAction.Activity("jobs.job.retried", "重試背景任務");

    /// <summary>「背景作業」頁的佇列：所有人的工作，含擁有者姓名與工作標籤（常是檔名），屬於查閱。</summary>
    public static readonly AuditAction QueueRead = AuditAction.Access("jobs.queue.read", "查詢背景工作佇列");
    public static readonly AuditAction QueueJobCancelled = AuditAction.Administration("jobs.queue_job.cancelled", "管理員停止背景工作");
}
