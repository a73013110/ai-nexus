namespace AiNexus.Features.Sharing;

/// <summary>在本程序內序列化分享的建立與撤銷。</summary>
public sealed class ShareWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
