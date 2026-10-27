using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Audit;

/// <summary>活動稽核模組自己的稽核動作：查閱稽核也要留證（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class AuditAudit
{
    public static readonly AuditAction EventRead = AuditAction.Access("audit.event.read", "查詢活動稽核");
    public static readonly AuditAction EventExported = AuditAction.Access("audit.event.exported", "匯出活動稽核");
    public static readonly AuditAction UsageRead = AuditAction.Access("audit.usage.read", "查詢功能使用量");
}
