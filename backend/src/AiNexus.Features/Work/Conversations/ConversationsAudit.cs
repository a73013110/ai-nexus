using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Conversations;

/// <summary>對話清單與管理員檢視對話的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class ConversationsAudit
{
    public static readonly AuditAction ConversationCreated = AuditAction.Activity("chat.conversation.created", "建立對話");
    public static readonly AuditAction ConversationImported = AuditAction.Activity("chat.conversation.imported", "匯入對話");
    public static readonly AuditAction ConversationRenamed = AuditAction.Activity("chat.conversation.renamed", "重新命名對話");
    public static readonly AuditAction ConversationUpdated = AuditAction.Activity("chat.conversation.updated", "整理對話");
    public static readonly AuditAction UserConversationRead = AuditAction.Access("identity.user_conversation.read", "檢視使用者對話清單");
    public static readonly AuditAction ConversationContentRead = AuditAction.Access("identity.conversation_content.read", "檢視使用者對話內容");
}
