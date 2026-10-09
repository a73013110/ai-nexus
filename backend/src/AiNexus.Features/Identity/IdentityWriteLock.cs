namespace AiNexus.Features.Identity;

public sealed class IdentityWriteLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
