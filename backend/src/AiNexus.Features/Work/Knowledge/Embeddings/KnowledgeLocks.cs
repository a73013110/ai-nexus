using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Work.Knowledge.Embeddings;

/// <summary>知識庫的行政寫入鎖（<see cref="AuditTarget.Locks"/>）。</summary>
public static class KnowledgeLocks
{
    /// <summary>「同時只有一個 active 的 embedding profile」跨多列；唯一索引是最後防線。</summary>
    public const string EmbeddingProfile = "knowledge:embedding-profile";
}
