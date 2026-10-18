using AiNexus.Data.App;
using AiNexus.Data.App.Collaboration;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge;

/// <summary>
/// 知識庫總覽：來源文件依處理狀態統計，並列出索引分段。只計入加入知識庫的文件；私人讀取與專案文件不算知識庫文件。
/// </summary>
internal sealed class KnowledgeDashboard(AppDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Knowledge;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var resources = db.Set<Resource>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var collections = await resources.CountAsync(x => x.Kind == Collection.Kind, ct);
        var documents = db.Set<Document>().AsNoTracking().Where(d => !d.IsDeleted && d.CollectionId != null && resources.Any(x => x.Id == d.Id));
        var status = await documents.GroupBy(x => 1).Select(g => new
        {
            All = g.Count(),
            Ready = g.Count(x => x.Status == "ready"),
            Failed = g.Count(x => x.Status == "failed"),
            Pages = g.Sum(x => x.PageCount),
        }).SingleOrDefaultAsync(ct);
        var chunks = await db.Set<Chunk>().AsNoTracking().CountAsync(x => documents.Any(d => d.Id == x.DocumentId), ct);
        var added = await DashboardDays.CountAsync(
            resources.Where(x => x.Kind == "document" && documents.Any(d => d.Id == x.Id)).Select(x => x.CreatedAt), scope, ct);
        var all = status?.All ?? 0; var ready = status?.Ready ?? 0; var failed = status?.Failed ?? 0;
        var processing = all - ready - failed;
        var section = new DashboardSection("knowledge", Feature)
            .Count("collections", "知識庫", collections, "個")
            .Count("documents", "知識庫文件", all, "份", hint: $"{ready} 份可查詢 · {(status?.Pages ?? 0):N0} 頁")
            .Count("chunks", "索引分段", chunks, "段")
            .Count("processing", "處理中文件", processing, "份", processing > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Count("failed", "處理失敗", failed, "份", failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Period("added", "期間新增文件", added.Sum(x => x.Value), "份")
            .Daily("added", "每日新增知識庫文件", "份", added)
            .Share("ready", "可查詢", ready, DashboardTones.Success)
            .Share("processing", "處理中", processing, DashboardTones.Info)
            .Share("failed", "處理失敗", failed, DashboardTones.Danger)
            .ActiveWhen(processing);
        var recent = await db.Set<Resource>().AsNoTracking().Where(x => x.OwnerId == scope.Actor && x.Kind == Collection.Kind)
            .OrderByDescending(x => x.UpdatedAt).Take(5).Select(x => new { x.Id, x.Name, x.UpdatedAt }).ToListAsync(ct);
        foreach (var item in recent) section.Item(item.Id.ToString(), item.Name, null, item.UpdatedAt, "/knowledge/" + item.Id);
        return section.Build();
    }
}
