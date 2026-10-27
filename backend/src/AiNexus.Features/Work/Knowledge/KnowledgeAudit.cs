using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Knowledge;

/// <summary>知識庫的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class KnowledgeAudit
{
    public static readonly AuditAction CollectionRead = AuditAction.Access("knowledge.collection.read", "查詢知識庫");
    public static readonly AuditAction CollectionCreated = AuditAction.Activity("knowledge.collection.created", "建立知識庫");
    public static readonly AuditAction CollectionUpdated = AuditAction.Activity("knowledge.collection.updated", "修改知識庫");
    public static readonly AuditAction CollectionDeleted = AuditAction.Activity("knowledge.collection.deleted", "刪除知識庫");
    public static readonly AuditAction CollectionAccessRead = AuditAction.Access("knowledge.collection_access.read", "檢視知識庫權限");
    public static readonly AuditAction CollectionShared = AuditAction.Activity("knowledge.collection.shared", "設定知識庫權限");
    public static readonly AuditAction ConversationCollectionUpdated = AuditAction.Activity("knowledge.conversation_collection.updated", "設定對話使用的知識庫");
    public static readonly AuditAction DocumentRead = AuditAction.Access("knowledge.document.read", "檢視文件");
    public static readonly AuditAction DocumentDownloaded = AuditAction.Access("knowledge.document.downloaded", "下載文件原檔");
    public static readonly AuditAction DocumentCreated = AuditAction.Activity("knowledge.document.created", "加入文件");
    public static readonly AuditAction DocumentUpdated = AuditAction.Activity("knowledge.document.updated", "修改文字文件");
    public static readonly AuditAction DocumentDeleted = AuditAction.Activity("knowledge.document.deleted", "刪除文件");
    public static readonly AuditAction DocumentIndexRebuilt = AuditAction.Activity("knowledge.document_index.rebuilt", "重新索引文件");
    public static readonly AuditAction DocumentSearched = AuditAction.Access("knowledge.document.searched", "搜尋知識庫");

    public static readonly AuditAction ProfileRead = AuditAction.Access("knowledge.embedding_profile.read", "查詢向量模型設定");
    public static readonly AuditAction ProfileActivated = AuditAction.Administration("knowledge.embedding_profile.activated", "啟用向量模型");
    public static readonly AuditAction ProfileRebuilt = AuditAction.Administration("knowledge.embedding_profile.rebuilt", "重建向量索引");
    public static readonly AuditAction ProfileCleared = AuditAction.Administration("knowledge.embedding_profile.cleared", "清除停用的向量");
    public static readonly AuditAction RetrievalModelDiagnosed = AuditAction.Administration("knowledge.retrieval_model.diagnosed", "測試檢索模型");
    public static readonly AuditAction RetrievalSearched = AuditAction.Access("knowledge.retrieval.searched", "檢索測試");
}
