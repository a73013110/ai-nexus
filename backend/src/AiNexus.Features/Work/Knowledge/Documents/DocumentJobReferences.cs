using AiNexus.Data.App;
using AiNexus.Features.Work.Jobs.Retention;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>
/// 文件記著它目前的處理工作（<c>Documents.JobId</c>，沒有外鍵）：處理失敗的文件靠它顯示進度與失敗原因（<see cref="GetDocumentJob"/>），
/// 工作刪掉後那個端點會回找不到。重新處理時 <c>JobId</c> 換成新的工作，舊的就不再被引用。
/// </summary>
internal sealed class DocumentJobReferences : IJobReferences
{
    public IQueryable<Guid?> JobIds(AppDbContext db) => db.Documents.Where(x => x.JobId != null).Select(x => x.JobId);
}
