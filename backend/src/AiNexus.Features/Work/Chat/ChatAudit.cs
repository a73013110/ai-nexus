using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Chat;

/// <summary>對話與生成的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class ChatAudit
{
    public static readonly AuditAction ConversationRead = AuditAction.Access("chat.conversation.read", "開啟對話");
    public static readonly AuditAction ConversationExported = AuditAction.Access("chat.conversation.exported", "匯出對話");
    public static readonly AuditAction ConversationDeleted = AuditAction.Activity("chat.conversation.deleted", "刪除對話");
    public static readonly AuditAction ConversationDuplicated = AuditAction.Activity("chat.conversation.duplicated", "複製對話");
    public static readonly AuditAction RunCreated = AuditAction.Activity("chat.run.created", "送出訊息");
    public static readonly AuditAction RunCancelled = AuditAction.Activity("chat.run.cancelled", "停止生成");
    // 背景工作：生成結束與中斷復原。
    public static readonly AuditAction RunFinished = AuditAction.Activity("chat.run.finished", "生成結束");
    public static readonly AuditAction RunRecovered = AuditAction.Activity("chat.run.recovered", "處理中斷的生成");
}
