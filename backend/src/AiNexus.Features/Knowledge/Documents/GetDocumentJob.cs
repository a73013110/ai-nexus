using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge.Documents;

public sealed record DocumentJobDto(JobDto Job, bool CanControl);

/// <summary>使用者可讀取文件的目前處理工作；只有工作擁有者且仍具編輯權的人，才能控制（例如取消）此工作。</summary>
internal sealed class GetDocumentJob(AppDbContext db, DocumentAccess documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/job", (Guid id, ICurrentUser user, GetDocumentJob handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DocumentJob");

    public async Task<Result<DocumentJobDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == document.Value.JobId, ct);
        if (job is null) return KnowledgeErrors.DocumentNotFound;
        var info = await documents.DescribeAsync(actor, document.Value, ct);
        if (!info.IsSuccess) return info.Error;
        return new DocumentJobDto(JobService.Describe(job), job.OwnerId == actor && info.Value.CanEdit);
    }
}
