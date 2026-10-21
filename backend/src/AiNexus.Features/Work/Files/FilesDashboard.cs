using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Files.Storage;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Files;

/// <summary>
/// 檔案庫總覽：已儲存的原始檔（同一檔案被多處重複使用也只計一次）、依類型的大小、每日上傳量，
/// 以及單一擁有者的儲存配額使用量。查詢在 <c>files.usp_GetFilesDashboard</c>，配額沿用 <see cref="AttachmentQuota"/>。
/// </summary>
internal sealed class FilesDashboard(ISqlDatabase<AppDbContext> sql, AttachmentQuota quota) : IDashboardSource
{
    public string Feature => FeatureCatalog.Files;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, uploads, types) = await sql.QueryDashboardAsync("files.usp_GetFilesDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.SharesAsync()), ct);
        var section = new DashboardSection("files", Feature)
            .Count("files", "已保存原檔", counts.Files, "個")
            .Size("size", "原檔大小", counts.Bytes)
            .Period("uploads", "期間保存", uploads.Sum(x => x.Value), "個")
            .Daily("uploads", "每日保存原檔", "個", uploads);
        foreach (var kind in types.GroupBy(x => Category(x.Key)).OrderByDescending(g => g.Sum(x => x.Value)))
            section.Share(kind.Key, kind.Key, kind.Sum(x => x.Value));
        if (scope.Owner is Guid id)
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

    private sealed record Counts(int Files, long Bytes);
}
