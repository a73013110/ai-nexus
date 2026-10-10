using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge.Documents;

public sealed record DocumentJobDto(JobDto Job, bool CanControl);

/// <summary>The current processing job of a document the user may read; only its owner who may still edit the document controls it.</summary>
internal sealed class GetDocumentJob(NexusDbContext db, DocumentAccess documents)
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
