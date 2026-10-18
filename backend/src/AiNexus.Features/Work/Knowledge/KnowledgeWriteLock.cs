namespace AiNexus.Features.Work.Knowledge;

public sealed class KnowledgeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
