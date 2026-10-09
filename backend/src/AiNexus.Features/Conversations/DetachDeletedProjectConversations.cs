using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

/// <summary>
/// A deleted project's conversations stay with their owners; only the project link is cleared, in the deleting
/// transaction. Deleted conversations count too, and an answer still generating in any of them blocks the deletion.
/// </summary>
internal sealed class DetachDeletedProjectConversations(NexusDbContext db) : IDomainEventHandler<ContainerDeleted>
{
    public async Task HandleAsync(ContainerDeleted e, CancellationToken ct)
    {
        if (e.Kind != "project") return;
        var conversations = db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ProjectId == e.ContainerId);
        if (await db.Runs.AnyAsync(r => r.ActiveOwnerId != null && conversations.Any(c => c.Id == r.ConversationId), ct))
            throw new ApiException(409, "generation_active", "專案仍有正在生成的回答，請先完成或停止，再刪除。");
        await conversations.ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
    }
}
