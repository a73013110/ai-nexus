using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Diagnostics;

/// <summary>診斷模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class DiagnosticsAudit
{
    public static readonly AuditAction LogRead = AuditAction.Access("diagnostics.log.read", "查詢系統日誌");
    public static readonly AuditAction LogDetailRead = AuditAction.Access("diagnostics.log_detail.read", "檢視日誌詳情");
    public static readonly AuditAction LogExported = AuditAction.Access("diagnostics.log.exported", "匯出系統日誌");
    // 系統動作：啟動後第一次清理時記下生效的日誌設定（擁有者為空識別碼）。
    public static readonly AuditAction ConfigurationActivated = AuditAction.Administration("diagnostics.configuration.activated", "套用日誌設定");
}
