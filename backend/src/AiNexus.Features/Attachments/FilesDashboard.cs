using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments.Storage;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments;

/// <summary>
/// Overview of the file library: saved originals (each counted once, however many places reuse it), their size by
/// kind, uploads per day and, for one owner, how much of the storage quota is used.
/// </summary>
internal sealed class FilesDashboard(NexusDbContext db, AttachmentQuota quota) : IDashboardSource
{
    public string Feature => FeatureIds.Files;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var library = db.Set<Attachment>().AsNoTracking()
            .Where(x => x.InLibrary && x.StorageState == AttachmentStates.Ready && (owner == null || x.OwnerId == owner));
        var types = await library.GroupBy(x => x.ContentType).Select(g => new { Type = g.Key, Count = g.Count(), Bytes = g.Sum(x => x.Size) }).ToListAsync(ct);
        var uploads = await DashboardDays.CountAsync(db, library.Select(x => x.CreatedAt), scope, ct);
        var section = new DashboardSection("files", Feature)
            .Count("files", "已保存原檔", types.Sum(x => x.Count), "個")
            .Size("size", "原檔大小", types.Sum(x => x.Bytes))
            .Period("uploads", "期間保存", uploads.Sum(x => x.Value), "個")
            .Daily("uploads", "每日保存原檔", "個", uploads);
        foreach (var kind in types.GroupBy(x => Category(x.Type)).OrderByDescending(g => g.Sum(x => x.Count)))
            section.Share(kind.Key, kind.Key, kind.Sum(x => x.Count));
        if (owner is Guid id)
        {
            var storage = await quota.ForAsync(id, ct);
            var used = storage.LimitBytes > 0 ? 100.0 * storage.UsedBytes / storage.LimitBytes : (double?)null;
            section.Metric(new("quota", "容量使用", used, "", DashboardFormats.Percent,
                used >= 90 ? DashboardTones.Danger : used >= 75 ? DashboardTones.Warning : DashboardTones.Neutral, false,
                "含對話附件與草稿暫存；上限依個人、群組或預設設定"));
        }
        return section.Build();
    }

    private static string Category(string type) => type switch
    {
        _ when type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => "圖片",
        "application/pdf" => "PDF",
        _ when type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) => "文字",
        _ when type.Contains("officedocument", StringComparison.OrdinalIgnoreCase) || type.Contains("msword", StringComparison.OrdinalIgnoreCase)
            || type.Contains("ms-excel", StringComparison.OrdinalIgnoreCase) || type.Contains("ms-powerpoint", StringComparison.OrdinalIgnoreCase) => "Office 文件",
        _ => "其他",
    };
}
