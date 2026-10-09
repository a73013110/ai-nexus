namespace AiNexus.Features.Sharing;

/// <summary>Serializes share creation and revocation in this process.</summary>
public sealed class ShareWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
