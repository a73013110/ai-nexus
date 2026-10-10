using AiNexus.Platform.Errors;

namespace AiNexus.Features.Collaboration;

/// <summary>
/// Another module's reason to refuse deleting a container, asked inside the deleting transaction before anything is
/// written. Implemented by the module that owns the blocking records (Conversations: an answer still generating).
/// </summary>
public interface IContainerDeletionCheck
{
    Task<Result> CheckAsync(string kind, Guid containerId, CancellationToken ct);
}
