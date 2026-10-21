using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories;

/// <summary>
/// Gitea 使用概況：已連線帳號、期間內啟動的 AI review，以及仍在執行中的數量。
/// 未設定 Gitea 時沒有可統計的內容，因此不顯示此區塊。查詢在 <c>repositories.usp_GetRepositoriesDashboard</c>。
/// </summary>
internal sealed class RepositoriesDashboard(ISqlDatabase<AppDbContext> sql, IOptions<GiteaOptions> gitea) : IDashboardSource
{
    public string Feature => FeatureCatalog.Repositories;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        if (!gitea.Value.Enabled) return null;
        var (counts, daily, status, recent) = await sql.QueryDashboardAsync("repositories.usp_GetRepositoriesDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync(), await r.SharesAsync(), await r.ItemsAsync<Review>()), ct);
        int Sum(Func<string, bool> match) => status.Where(x => match(x.Key)).Sum(x => x.Value);
        var failed = Sum(x => x == RunStates.Failed);
        var section = new DashboardSection("repositories", Feature)
            .Count("connections", "已連線帳號", counts.Connections, "個")
            .Count("running", "Review 進行中", counts.Running, "次", counts.Running > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Period("reviews", "期間 Review", daily.Sum(x => x.Value), "次")
            .Period("failed", "Review 失敗", failed, "次", tone: failed > 0 ? DashboardTones.Danger : DashboardTones.Neutral)
            .Daily("reviews", "每日 AI review", "次", daily)
            .Share("completed", "完成", Sum(x => x == RunStates.Completed), DashboardTones.Success)
            .Share("active", "進行中", Sum(RunStates.IsActive), DashboardTones.Info)
            .Share("failed", "失敗", failed, DashboardTones.Danger)
            .Share("cancelled", "已取消", Sum(x => x == RunStates.Cancelled))
            .ActiveWhen(counts.Running);
        foreach (var review in recent)
            section.Item(review.Id.ToString(), review.Repository, review.Commit[..Math.Min(7, review.Commit.Length)], review.CreatedAt, FeatureCatalog.RouteOf(FeatureCatalog.Repositories) + "/" + review.Repository + "/reviews/" + review.Id);
        return section.Build();
    }

    private sealed record Counts(int Connections, int Running);

    private sealed record Review(Guid Id, string Repository, string Commit, DateTimeOffset CreatedAt);
}
