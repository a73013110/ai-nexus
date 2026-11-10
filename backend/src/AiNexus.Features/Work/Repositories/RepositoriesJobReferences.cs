using AiNexus.Data.App;
using AiNexus.Features.Work.Jobs.Retention;

namespace AiNexus.Features.Work.Repositories;

/// <summary>程式碼 Review 與資料夾匯入記著執行它的工作（有外鍵）；紀錄還在，工作就留著。</summary>
internal sealed class RepositoriesJobReferences : IJobReferences
{
    public IQueryable<Guid?> JobIds(AppDbContext db)
        => db.RepositoryReviews.Select(x => (Guid?)x.JobId).Concat(db.RepositoryFolderImports.Select(x => (Guid?)x.JobId));
}
