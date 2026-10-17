namespace AiNexus.Features.Attachments;

// 序列化配額檢查與附件寫入；呼叫模型供應商時絕不持有此閘門。
public sealed class AttachmentWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
