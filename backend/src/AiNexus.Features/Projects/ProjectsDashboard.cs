using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects.Templates;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>Overview of projects: owned, archived, prompt templates and, for one owner, projects shared with them.</summary>
internal sealed class ProjectsDashboard(NexusDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Projects;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var resources = db.Set<WorkspaceResource>().AsNoTracking().Where(x => x.Kind == "project" && (owner == null || x.OwnerId == owner));
        var projects = db.Set<Project>().AsNoTracking().Where(p => resources.Any(r => r.Id == p.Id));
        var counts = await projects.GroupBy(x => 1).Select(g => new { All = g.Count(), Archived = g.Count(x => x.IsArchived) }).SingleOrDefaultAsync(ct);
        var templates = await db.Set<ProjectTemplate>().AsNoTracking().CountAsync(t => projects.Any(p => p.Id == t.ProjectId), ct);
        var created = await DashboardDays.CountAsync(db, resources.Select(x => x.CreatedAt), scope, ct);
        var all = counts?.All ?? 0; var archived = counts?.Archived ?? 0;
        var section = new DashboardSection("projects", Feature)
            .Count("projects", "專案", all - archived, "個", hint: archived > 0 ? $"另有 {archived} 個封存" : null)
            .Count("templates", "提問範本", templates, "個")
            .Period("created", "期間建立", created.Sum(x => x.Value), "個")
            .Daily("created", "每日建立專案", "個", created);
        if (owner is Guid id)
        {
            var shared = await db.Set<ResourceMember>().AsNoTracking()
                .CountAsync(m => m.UserId == id && db.Set<WorkspaceResource>().Any(r => r.Id == m.ResourceId && r.Kind == "project" && r.OwnerId != id), ct);
            section.Count("shared", "共用給我", shared, "個");
        }
        section.Share("active", "進行中", all - archived, DashboardTones.Info).Share("archived", "已封存", archived);
        var recent = await db.Set<WorkspaceResource>().AsNoTracking().Where(x => x.Kind == "project" && x.OwnerId == scope.Actor)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new { x.Id, x.Name, x.UpdatedAt }).ToListAsync(ct);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, "/projects/" + item.Id);
        return section.Build();
    }
}
