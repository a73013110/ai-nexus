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
internal sealed class DetachDeletedProjectConversations(NexusDbContext db) : IDomainEventHandler<ContainerDeleted>, IContainerDeletionCheck
{
    public async Task<Result> CheckAsync(string kind, Guid containerId, CancellationToken ct)
        => kind == "project" && await db.Runs.AnyAsync(r => r.ActiveOwnerId != null && Conversations(containerId).Any(c => c.Id == r.ConversationId), ct)
            ? ConversationsErrors.GenerationActive : Result.Success;

    public async Task HandleAsync(ContainerDeleted e, CancellationToken ct)
    {
        if (e.Kind != "project") return;
        await Conversations(e.ContainerId).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
    }

    private IQueryable<Conversation> Conversations(Guid project) => db.Conversations.IgnoreQueryFilters([SoftDelete.Filter]).Where(x => x.ProjectId == project);
}
