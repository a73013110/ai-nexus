using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using AiNexus.Platform.Data.Sql;

namespace AiNexus.Features.Admin.Quality;

/// <summary>
/// 回答品質總覽：期間內的有幫助／沒幫助評分數與發起的評測次數；只計入評分，理由與備註仍只對作者可見。
/// 查詢在 <c>quality.usp_GetQualityDashboard</c>。
/// </summary>
internal sealed class QualityDashboard(ISqlDatabase<AppDbContext> sql) : IDashboardSource
{
    public string Feature => FeatureCatalog.Quality;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var (counts, daily) = await sql.QueryDashboardAsync("quality.usp_GetQualityDashboard", scope,
            async r => (await r.CountsAsync<Counts>(), await r.DaysAsync()), ct);
        var (up, down) = (counts.Up, counts.Down);
        double? helpful = up + down > 0 ? 100.0 * up / (up + down) : null;
        return new DashboardSection("quality", Feature)
            .Period("helpful", "回覆有幫助", helpful, "", DashboardFormats.Percent,
                helpful < 60 ? DashboardTones.Warning : DashboardTones.Neutral, up + down > 0 ? $"{up} 則有幫助 · {down} 則沒幫助" : "期間尚無評分")
            .Period("ratings", "期間評分", up + down, "則")
            .Period("evaluations", "期間評測", counts.Evaluations + counts.Retrievals, "次", hint: $"{counts.Evaluations} 次模型／指令 · {counts.Retrievals} 次檢索")
            .Count("running", "評測進行中", counts.Running, "次", counts.Running > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Daily("ratings", "每日評分", "則", daily)
            .Share("up", "有幫助", up, DashboardTones.Success)
            .Share("down", "沒幫助", down, DashboardTones.Danger)
            .ActiveWhen(counts.Running)
            .Build();
    }

    private sealed record Counts(int Up, int Down, int Evaluations, int Retrievals, int Running);
}
