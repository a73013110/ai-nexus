using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Mail;

/// <summary>郵件模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class MailAudit
{
    public static readonly AuditAction MessageRead = AuditAction.Access("mail.message.read", "查詢郵件寄送紀錄");
    public static readonly AuditAction MessageSent = AuditAction.Administration("mail.message.sent", "重寄郵件");
    public static readonly AuditAction MessageRetried = AuditAction.Administration("mail.message.retried", "立即重試郵件");
    public static readonly AuditAction TestMessageSent = AuditAction.Administration("mail.test_message.sent", "寄送測試信");
    public static readonly AuditAction TransportDiagnosed = AuditAction.Administration("mail.transport.diagnosed", "郵件連線診斷");
    public static readonly AuditAction SettingsUpdated = AuditAction.Administration("mail.settings.updated", "調整郵件寄送設定");
}
