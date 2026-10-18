using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Projects;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Projects;

/// <summary>專案總覽：自己擁有的、已封存的、提示詞範本數，以及單一擁有者時別人分享給他的專案。</summary>
internal sealed class ProjectsDashboard(AppDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Projects;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var resources = db.Set<Resource>().AsNoTracking().Where(x => x.Kind == "project" && (owner == null || x.OwnerId == owner));
        var projects = db.Set<Project>().AsNoTracking().Where(p => resources.Any(r => r.Id == p.Id));
        var counts = await projects.GroupBy(x => 1).Select(g => new { All = g.Count(), Archived = g.Count(x => x.IsArchived) }).SingleOrDefaultAsync(ct);
        var templates = await db.Set<ProjectTemplate>().AsNoTracking().CountAsync(t => projects.Any(p => p.Id == t.ProjectId), ct);
        var created = await DashboardDays.CountAsync(resources.Select(x => x.CreatedAt), scope, ct);
        var all = counts?.All ?? 0;
        var archived = counts?.Archived ?? 0;
        var section = new DashboardSection("projects", Feature)
            .Count("projects", "專案", all - archived, "個", hint: archived > 0 ? $"另有 {archived} 個封存" : null)
            .Count("templates", "提問範本", templates, "個")
            .Period("created", "期間建立", created.Sum(x => x.Value), "個")
            .Daily("created", "每日建立專案", "個", created);
        if (owner is Guid id)
        {
            var shared = await db.Set<ResourceMember>().AsNoTracking()
                .CountAsync(m => m.UserId == id && db.Set<Resource>().Any(r => r.Id == m.ResourceId && r.Kind == "project" && r.OwnerId != id), ct);
            section.Count("shared", "共用給我", shared, "個");
        }
        section.Share("active", "進行中", all - archived, DashboardTones.Info).Share("archived", "已封存", archived);
        var recent = await db.Set<Resource>().AsNoTracking().Where(x => x.Kind == "project" && x.OwnerId == scope.Actor)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new { x.Id, x.Name, x.UpdatedAt }).ToListAsync(ct);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, "/projects/" + item.Id);
        return section.Build();
    }
}
