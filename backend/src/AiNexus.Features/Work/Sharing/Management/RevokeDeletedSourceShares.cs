using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Artifacts.Management;
using AiNexus.Features.Work.Chat.Transcripts;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Work.Sharing.Management;

/// <summary>被刪除的對話或成果，其分享會在刪除交易中、儲存之前就失效。</summary>
internal sealed class RevokeDeletedSourceShares(ShareService shares) : IDomainEventHandler<ConversationDeleted>, IDomainEventHandler<ArtifactDeleted>
{
    public Task HandleAsync(ConversationDeleted e, CancellationToken ct) => shares.RevokeSourceAsync(ShareAccess.ConversationKind, e.ConversationId, ct);

    public Task HandleAsync(ArtifactDeleted e, CancellationToken ct) => shares.RevokeSourceAsync(Artifact.Kind, e.ArtifactId, ct);
}
