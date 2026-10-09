namespace AiNexus.Features.Repositories;

/// <summary>Serializes the module's writes (connection, imports, review creation) within one host.</summary>
public sealed class RepositoryWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
