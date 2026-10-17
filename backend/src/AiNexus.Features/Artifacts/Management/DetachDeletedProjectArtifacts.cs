using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts.Management;

/// <summary>已刪除專案的成果仍歸原擁有者；只在刪除專案的交易中清除專案關聯。</summary>
internal sealed class DetachDeletedProjectArtifacts(NexusDbContext db) : IDomainEventHandler<ContainerDeleted>
{
    public async Task HandleAsync(ContainerDeleted e, CancellationToken ct)
    {
        if (e.Kind != "project") return;
        await db.Set<Artifact>().Where(x => x.ProjectId == e.ContainerId).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectId, (Guid?)null), ct);
    }
}
