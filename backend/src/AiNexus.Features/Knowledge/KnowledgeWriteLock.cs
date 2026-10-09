namespace AiNexus.Features.Knowledge;

public sealed class KnowledgeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
