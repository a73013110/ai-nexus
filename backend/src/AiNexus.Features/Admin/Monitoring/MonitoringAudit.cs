using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Monitoring;

/// <summary>即時監控模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class MonitoringAudit
{
    public static readonly AuditAction SnapshotRead = AuditAction.Access("monitoring.snapshot.read", "檢視即時監控");
    public static readonly AuditAction SnapshotExported = AuditAction.Access("monitoring.snapshot.exported", "匯出即時監控");
}
