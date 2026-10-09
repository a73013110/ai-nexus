using AiNexus.Features.Artifacts;
using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Sharing;

/// <summary>Shares of a deleted conversation or artifact stop working in the deleting transaction, before its save.</summary>
internal sealed class RevokeDeletedSourceShares(ShareService shares) : IDomainEventHandler<ConversationDeleted>, IDomainEventHandler<ArtifactDeleted>
{
    public Task HandleAsync(ConversationDeleted e, CancellationToken ct) => shares.RevokeSourceAsync("conversation", e.ConversationId, ct);

    public Task HandleAsync(ArtifactDeleted e, CancellationToken ct) => shares.RevokeSourceAsync(Artifact.Kind, e.ArtifactId, ct);
}
