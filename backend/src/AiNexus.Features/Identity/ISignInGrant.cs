namespace AiNexus.Features.Identity;

/// <summary>
/// A grant applied when a directory user signs in, such as the configured bootstrap administrators. Modules above
/// Identity implement it, so sign-in does not depend on them.
/// </summary>
public interface ISignInGrant
{
    /// <summary>Whether <see cref="ApplyAsync"/> would grant anything, so sign-in must take the identity write gate.</summary>
    Task<bool> PendingAsync(NexusUser user, CancellationToken ct);

    /// <summary>Called under the identity write gate.</summary>
    Task ApplyAsync(NexusUser user, CancellationToken ct);
}
