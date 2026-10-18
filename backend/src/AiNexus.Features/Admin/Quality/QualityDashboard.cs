using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Quality;

/// <summary>回答品質總覽：期間內的有幫助／沒幫助評分數與發起的評測次數；只計入評分，理由與備註仍只對作者可見。</summary>
internal sealed class QualityDashboard(AppDbContext db) : IDashboardSource
{
    public string Feature => FeatureIds.Quality;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        var owner = scope.Owner;
        var feedback = db.Set<MessageFeedback>().AsNoTracking()
            .Where(x => (owner == null || x.OwnerId == owner) && x.Rating != 0 && x.UpdatedAt >= scope.From && x.UpdatedAt < scope.Until);
        var ratings = await feedback.GroupBy(x => 1).Select(g => new { Up = g.Count(x => x.Rating > 0), Down = g.Count(x => x.Rating < 0) }).SingleOrDefaultAsync(ct);
        var daily = await DashboardDays.CountAsync(feedback.Select(x => x.UpdatedAt), scope, ct);
        var runs = db.Set<EvaluationRun>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var retrieval = db.Set<RetrievalEvaluation>().AsNoTracking().Where(x => owner == null || x.OwnerId == owner);
        var evaluations = await runs.CountAsync(x => x.CreatedAt >= scope.From && x.CreatedAt < scope.Until, ct);
        var retrievals = await retrieval.CountAsync(x => x.CreatedAt >= scope.From && x.CreatedAt < scope.Until, ct);
        var jobs = db.Set<BackgroundJob>().AsNoTracking();
        var active = await runs.CountAsync(r => jobs.Any(j => j.Id == r.JobId && (j.Status == "queued" || j.Status == "running")), ct)
            + await retrieval.CountAsync(r => jobs.Any(j => j.Id == r.JobId && (j.Status == "queued" || j.Status == "running")), ct);
        var up = ratings?.Up ?? 0; var down = ratings?.Down ?? 0;
        double? helpful = up + down > 0 ? 100.0 * up / (up + down) : null;
        return new DashboardSection("quality", Feature)
            .Period("helpful", "回覆有幫助", helpful, "", DashboardFormats.Percent,
                helpful < 60 ? DashboardTones.Warning : DashboardTones.Neutral, up + down > 0 ? $"{up} 則有幫助 · {down} 則沒幫助" : "期間尚無評分")
            .Period("ratings", "期間評分", up + down, "則")
            .Period("evaluations", "期間評測", evaluations + retrievals, "次", hint: $"{evaluations} 次模型／指令 · {retrievals} 次檢索")
            .Count("running", "評測進行中", active, "次", active > 0 ? DashboardTones.Info : DashboardTones.Neutral)
            .Daily("ratings", "每日評分", "則", daily)
            .Share("up", "有幫助", up, DashboardTones.Success)
            .Share("down", "沒幫助", down, DashboardTones.Danger)
            .ActiveWhen(active)
            .Build();
    }
}
