using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>使用者可讀取的知識庫文件，依檔名排序並附目前工作狀態；筆數受 MaxDocumentsPerCollection 上限限制。</summary>
internal sealed class ListKnowledgeDocuments(AppDbContext db, ResourceAccess access, IOptions<KnowledgeOptions> options)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections/{id:guid}/documents", (Guid id, ICurrentUser user, ListKnowledgeDocuments handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("ListKnowledgeDocuments");

    public async Task<Result<IReadOnlyList<DocumentDto>>> HandleAsync(Guid actor, Guid collection, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, collection, Collection.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var editable = (await access.DescribeAsync(actor, resource.Value, ct)).CanEdit;
        var docs = await db.Set<Document>().AsNoTracking().Where(x => x.CollectionId == collection && !x.IsDeleted)
            .LeftJoin(db.Set<BackgroundJob>(), x => x.JobId, job => job.Id, (document, job) => new { document, State = job == null ? null : job.Status })
            .OrderBy(x => x.document.FileName).Take(options.Value.Indexing.MaxDocumentsPerCollection).ToListAsync(ct);
        return docs.Select(x => DocumentAccess.Describe(x.document, editable, x.State)).ToList();
    }
}
