using AiNexus.Data.App;
using AiNexus.Features.Work.Jobs.Retention;

namespace AiNexus.Features.Admin.Quality;

/// <summary>評測與檢索評測的結果記著產生它的工作（有外鍵）；結果還在，工作就留著。</summary>
internal sealed class QualityJobReferences : IJobReferences
{
    public IQueryable<Guid?> JobIds(AppDbContext db)
        => db.EvaluationRuns.Select(x => (Guid?)x.JobId).Concat(db.RetrievalEvaluations.Select(x => (Guid?)x.JobId));
}
