using AiNexus.Data.App;
using AiNexus.Platform.Data;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>
/// 使用者對文件的操作（新增、編輯、重新索引、刪除）排隊用的資料庫鎖，鎖隨交易釋放，要是交易的第一個動作（<see cref="ApplicationLocks"/>）。
/// 知識庫內的文件鎖整個知識庫，因為規則跨文件（文件上限、刪除知識庫時取消所有工作），而協作者是不同人，擁有者列鎖擋不住；獨立文件只鎖自己。不同知識庫互不等待。
/// 背景工作不取這把鎖，由工作的租約與取消旗標隔開。
/// </summary>
[Scoped]
public sealed class DocumentWriteLock(AppDbContext db)
{
    // 持有者只做幾句 SQL，等這麼久表示異常，交給使用者重試。
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task<bool> TryAcquireForCollectionAsync(Guid collection, CancellationToken ct) => AcquireAsync(collection, ct);

    /// <summary>文件屬於哪個知識庫不會變，所以取鎖前可以直接讀；找不到文件時鎖它自己，由呼叫端接著回 not found。</summary>
    public async Task<bool> TryAcquireForDocumentAsync(Guid document, CancellationToken ct)
    {
        var collection = await db.Documents.Where(x => x.Id == document).Select(x => x.CollectionId).SingleOrDefaultAsync(ct);
        return await AcquireAsync(collection ?? document, ct);
    }

    private Task<bool> AcquireAsync(Guid scope, CancellationToken ct) => db.Database.TryAcquireApplicationLockAsync($"knowledge-documents:{scope:N}", Timeout, ct);
}
