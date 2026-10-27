using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Admin.Quality;

/// <summary>品質評測模組的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class QualityAudit
{
    public static readonly AuditAction SetRead = AuditAction.Access("quality.evaluation_set.read", "查詢評測題庫");
    public static readonly AuditAction SetCreated = AuditAction.Activity("quality.evaluation_set.created", "建立評測題庫");
    public static readonly AuditAction SetUpdated = AuditAction.Activity("quality.evaluation_set.updated", "修改評測題庫");
    public static readonly AuditAction SetDeleted = AuditAction.Activity("quality.evaluation_set.deleted", "刪除評測題庫");
    public static readonly AuditAction SetAccessRead = AuditAction.Access("quality.evaluation_set_access.read", "檢視評測題庫權限");
    public static readonly AuditAction SetShared = AuditAction.Activity("quality.evaluation_set.shared", "設定評測題庫權限");
    public static readonly AuditAction RunRead = AuditAction.Access("quality.evaluation_run.read", "查詢評測結果");
    public static readonly AuditAction RunQueued = AuditAction.Activity("quality.evaluation_run.queued", "執行評測");
    public static readonly AuditAction ResultReviewed = AuditAction.Activity("quality.evaluation_result.reviewed", "人工複核評測結果");
    public static readonly AuditAction FeedbackRead = AuditAction.Access("quality.feedback.read", "查詢回覆回饋");
    public static readonly AuditAction FeedbackUpdated = AuditAction.Activity("chat.message_feedback.updated", "回饋回覆品質");
    public static readonly AuditAction RetrievalRead = AuditAction.Access("quality.retrieval_evaluation.read", "查詢檢索評測");
    public static readonly AuditAction RetrievalQueued = AuditAction.Activity("quality.retrieval_evaluation.queued", "執行檢索評測");
}
