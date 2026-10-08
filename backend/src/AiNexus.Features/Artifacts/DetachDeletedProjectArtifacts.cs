using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

/// <summary>A deleted project's artifacts stay with their owners; only the project link is cleared, in the deleting transaction.</summary>
internal sealed class DetachDeletedProjectArtifacts(NexusDbContext db) : IDomainEventHandler<ContainerDeleted>
{
    public async Task HandleAsync(ContainerDeleted e, CancellationToken ct)
    {
        if (e.Kind != "project") return;
        await db.Set<Artifact>().Where(x => x.ProjectId == e.ContainerId).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
    }
}
