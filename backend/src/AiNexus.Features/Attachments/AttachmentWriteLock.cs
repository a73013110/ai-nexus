namespace AiNexus.Features.Attachments;

// Serializes quota checks and attachment writes; provider calls never hold this gate.
public sealed class AttachmentWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
