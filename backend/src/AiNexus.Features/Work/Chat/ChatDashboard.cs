using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Chat;

/// <summary>
/// 對話與 AI 生成模組的儀表板資料來源。
/// 彙整使用者的對話數量、提問量趨勢、生成成功/失敗/取消統計、平均回應時間以及使用模型分佈。
/// </summary>
internal sealed class ChatDashboard(AppDbContext db, ModelPresentation presentation) : IDashboardSource
{
    private const int ModelShares = 6;

    public string Feature => FeatureIds.Chat;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;

        // owner 為 null 代表全平台檢視，否則只計算該使用者自己的對話
        var conversations = db.Conversations
            .AsNoTracking()
            .Where(x => owner == null || x.OwnerId == owner);

        var counts = await conversations
            .GroupBy(x => 1)
            .Select(g => new
            {
                All = g.Count(),
                Favorites = g.Count(x => x.IsFavorite),
                Archived = g.Count(x => x.IsArchived)
            })
            .SingleOrDefaultAsync(ct);

        var questions = db.Messages
            .AsNoTracking()
            .Where(m => m.Role == "user" && conversations.Any(c => c.Id == m.ConversationId))
            .Select(m => m.CreatedAt);

        var daily = await DashboardDays.CountAsync(questions, scope, ct);

        var runs = db.Runs
            .AsNoTracking()
            .Where(x => owner == null || x.OwnerId == owner);

        var active = await runs.CountAsync(x => x.ActiveOwnerId != null, ct);
        var period = runs.Where(x => x.CreatedAt >= scope.From && x.CreatedAt < scope.Until);

        var outcomes = await period
            .GroupBy(x => 1)
            .Select(g => new
            {
                Completed = g.Count(x => x.Status == RunStates.Completed),
                Failed = g.Count(x => x.Status == RunStates.Failed),
                Cancelled = g.Count(x => x.Status == RunStates.Cancelled),
            })
            .SingleOrDefaultAsync(ct);

        // 失敗或取消的 Run 沒有可比較的完整耗時，只平均完成的回覆
        var latency = await period
            .Where(x => x.Status == RunStates.Completed && x.DurationMilliseconds != null)
            .AverageAsync(x => (double?)x.DurationMilliseconds, ct);

        var models = await period
            .GroupBy(x => x.ModelId)
            .Select(g => new { Model = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var failed = outcomes?.Failed ?? 0;

        var section = new DashboardSection("chat", Feature)
            .Count("conversations", "對話", counts?.All ?? 0, "個", hint: $"{counts?.Favorites ?? 0} 個收藏 · {counts?.Archived ?? 0} 個封存")
            .Count("generating", "AI 回覆生成", active, "件進行中", active > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Period("questions", "期間提問", daily.Sum(x => x.Value), "則")
            .Period("replies", "完成回覆", outcomes?.Completed ?? 0, "則", tone: DashboardTones.Success,
                hint: outcomes?.Cancelled > 0 ? $"另有 {outcomes.Cancelled} 則已停止" : null)
            .Period("failed", "回覆失敗", failed, "則", tone: failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Period("latency", "平均回覆時間", latency, "", DashboardFormats.Duration, hint: "完成的回覆，從送出到結束")
            .Daily("questions", "每日提問", "則", daily)
            .ActiveWhen(active);

        foreach (var model in models.Take(ModelShares))
        {
            section.Share(model.Model, presentation.DisplayName(model.Model, scope.Platform) ?? "已停用的模型", model.Count);
        }

        section.Share("other", "其他模型", models.Skip(ModelShares).Sum(x => x.Count));

        var recent = await db.Conversations
            .AsNoTracking()
            .Where(x => x.OwnerId == scope.Actor && !x.IsArchived)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(6)
            .Select(x => new { x.Id, x.Title, x.UpdatedAt, x.IsFavorite })
            .ToListAsync(ct);

        foreach (var item in recent)
        {
            section.Item(item.Id.ToString(), item.Title, item.IsFavorite ? "收藏" : null, item.UpdatedAt, "/chat/" + item.Id);
        }

        return section.Build();
    }
}
