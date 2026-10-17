using AiNexus.Features.Artifacts;
using AiNexus.Features.Artifacts.Management;
using AiNexus.Features.Chat.Transcripts;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Sharing.Management;

/// <summary>被刪除的對話或成果，其分享會在刪除交易中、儲存之前就失效。</summary>
internal sealed class RevokeDeletedSourceShares(ShareService shares) : IDomainEventHandler<ConversationDeleted>, IDomainEventHandler<ArtifactDeleted>
{
    public Task HandleAsync(ConversationDeleted e, CancellationToken ct) => shares.RevokeSourceAsync("conversation", e.ConversationId, ct);

    public Task HandleAsync(ArtifactDeleted e, CancellationToken ct) => shares.RevokeSourceAsync(Artifact.Kind, e.ArtifactId, ct);
}
