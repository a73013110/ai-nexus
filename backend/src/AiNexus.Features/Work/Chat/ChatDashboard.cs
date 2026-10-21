using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Work.Chat;

/// <summary>
/// 對話與 AI 生成模組的儀表板資料來源。
/// 彙整使用者的對話數量、提問量趨勢、生成成功/失敗/取消統計、平均回應時間以及使用模型分佈；查詢在 <c>conversations.usp_GetChatDashboard</c>。
/// </summary>
internal sealed class ChatDashboard(ISqlDatabase<AppDbContext> sql, ModelPresentation presentation) : IDashboardSource
{
    private const int ModelShares = 6;

    public string Feature => FeatureCatalog.Chat;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, daily, models, recent) = await sql.QueryDashboardAsync("conversations.usp_GetChatDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.SharesAsync(), await r.ItemsAsync<Recent>()), ct);
        var section = new DashboardSection("chat", Feature)
            .Count("conversations", "對話", counts.Conversations, "個", hint: $"{counts.Favorites} 個收藏 · {counts.Archived} 個封存")
            .Count("generating", "AI 回覆生成", counts.Generating, "件進行中", counts.Generating > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Period("questions", "期間提問", daily.Sum(x => x.Value), "則")
            .Period("replies", "完成回覆", counts.Completed, "則", tone: DashboardTones.Success,
                hint: counts.Cancelled > 0 ? $"另有 {counts.Cancelled} 則已停止" : null)
            .Period("failed", "回覆失敗", counts.Failed, "則", tone: counts.Failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Period("latency", "平均回覆時間", counts.LatencyMilliseconds, "", DashboardFormats.Duration, hint: "完成的回覆，從送出到結束")
            .Daily("questions", "每日提問", "則", daily)
            .ActiveWhen(counts.Generating);
        // procedure 依數量由多到少排序：前幾名各自一塊，其餘合併成「其他模型」。
        foreach (var model in models.Take(ModelShares))
            section.Share(model.Key, presentation.DisplayName(model.Key, scope.Platform) ?? "已停用的模型", model.Value);
        section.Share("other", "其他模型", models.Skip(ModelShares).Sum(x => x.Value));
        foreach (var item in recent)
            section.Item(item.Id.ToString(), item.Title, item.IsFavorite ? "收藏" : null, item.UpdatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Chat) + "/" + item.Id);
        return section.Build();
    }

    private sealed record Counts(int Conversations, int Favorites, int Archived, int Generating, int Completed, int Failed, int Cancelled, double? LatencyMilliseconds);

    private sealed record Recent(Guid Id, string Title, DateTimeOffset UpdatedAt, bool IsFavorite);
}
