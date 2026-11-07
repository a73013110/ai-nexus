using AiNexus.Data.App;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Mail;

/// <summary>郵件模組的稽核動作（ADR 0023）與共用快照。名稱是稽核合約，寫入後不可改。</summary>
public static class MailAudit
{
    public static readonly AuditAction MessageRead = AuditAction.Access("mail.message.read", "查詢郵件寄送紀錄");
    public static readonly AuditAction MessageSent = AuditAction.Administration("mail.message.sent", "重寄郵件");
    public static readonly AuditAction MessageRetried = AuditAction.Administration("mail.message.retried", "立即重試郵件");
    public static readonly AuditAction TestMessageSent = AuditAction.Administration("mail.test_message.sent", "寄送測試信");
    public static readonly AuditAction TransportDiagnosed = AuditAction.Administration("mail.transport.diagnosed", "郵件連線診斷");
    public static readonly AuditAction SettingsUpdated = AuditAction.Administration("mail.settings.updated", "調整郵件寄送設定");
    /// <summary>保存期清理（系統動作，擁有者為空）：只記各類筆數，沒有清到任何資料的那一輪不記。</summary>
    public static readonly AuditAction MessageCleared = AuditAction.Administration("mail.message.cleared", "依保存期清除郵件");

    /// <summary>重寄與測試信新建立的那封信：只記 ID、狀態與分類，不記收件者與內容；還沒建立時為 null。</summary>
    internal static async Task<object?> CreatedMessageAsync(AppDbContext db, Guid? created, CancellationToken ct)
        => created is { } id ? await db.OutboundMessages.AsNoTracking().Where(x => x.Id == id).Select(x => new { id = x.Id, status = x.Status, category = x.Category }).SingleOrDefaultAsync(ct) : null;
}
