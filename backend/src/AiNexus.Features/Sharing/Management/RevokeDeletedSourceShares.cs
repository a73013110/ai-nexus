using AiNexus.Features.Artifacts;
using AiNexus.Features.Artifacts.Management;
using AiNexus.Features.Chat.Transcripts;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Sharing.Management;

/// <summary>Shares of a deleted conversation or artifact stop working in the deleting transaction, before its save.</summary>
internal sealed class RevokeDeletedSourceShares(ShareService shares) : IDomainEventHandler<ConversationDeleted>, IDomainEventHandler<ArtifactDeleted>
{
    public Task HandleAsync(ConversationDeleted e, CancellationToken ct) => shares.RevokeSourceAsync("conversation", e.ConversationId, ct);

    public Task HandleAsync(ArtifactDeleted e, CancellationToken ct) => shares.RevokeSourceAsync(Artifact.Kind, e.ArtifactId, ct);
}
