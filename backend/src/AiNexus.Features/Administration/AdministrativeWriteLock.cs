namespace AiNexus.Features.Administration;

public sealed class AdministrativeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
