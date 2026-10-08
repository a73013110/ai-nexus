namespace AiNexus.Platform.Security;

/// <summary>The signed-in user of the current request once the identity module has resolved it.</summary>
/// <remarks>Infrastructure uses it only for correlation (logs, traces); authorization never relies on it.</remarks>
public interface IRequestUser
{
    Guid? ResolvedId { get; }
}
