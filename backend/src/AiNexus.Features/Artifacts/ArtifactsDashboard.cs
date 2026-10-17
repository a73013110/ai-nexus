using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Artifacts;

/// <summary>成果文件總覽：目前數量，以及期間內保存的不可變版本數。</summary>
internal sealed class ArtifactsDashboard(NexusDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Artifacts;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var resources = db.Set<WorkspaceResource>().AsNoTracking().Where(x => x.Kind == Artifact.Kind && (owner == null || x.OwnerId == owner));
        var counts = await db.Set<Artifact>().AsNoTracking().Where(a => resources.Any(r => r.Id == a.Id)).GroupBy(x => 1)
            .Select(g => new { All = g.Count(), InProjects = g.Count(x => x.ProjectId != null), FromChat = g.Count(x => x.SourceMessageId != null) })
            .SingleOrDefaultAsync(ct);
        // 版本數依作者計算：協作者的編輯算在協作者名下，而不是成果擁有者。
        var revisions = db.Set<ArtifactRevision>().AsNoTracking().Where(x => owner == null || x.AuthorId == owner);
        var saved = await DashboardDays.CountAsync(db, revisions.Select(x => x.CreatedAt), scope, ct);
        var all = counts?.All ?? 0;
        var projects = counts?.InProjects ?? 0;
        var section = new DashboardSection("artifacts", Feature)
            .Count("artifacts", "成果文件", all, "份", hint: $"{counts?.FromChat ?? 0} 份由對話建立")
            .Period("versions", "期間保存版本", saved.Sum(x => x.Value), "版")
            .Daily("versions", "每日保存版本", "版", saved)
            .Share("projects", "屬於專案", projects, DashboardTones.Info)
            .Share("personal", "個人", all - projects);
        var recent = await db.Set<WorkspaceResource>().AsNoTracking().Where(x => x.Kind == Artifact.Kind && x.OwnerId == scope.Actor)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new { x.Id, x.Name, x.UpdatedAt }).ToListAsync(ct);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, "/artifacts/" + item.Id);
        return section.Build();
    }
}
