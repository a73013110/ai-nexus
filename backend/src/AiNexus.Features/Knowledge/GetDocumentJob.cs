using AiNexus.Features.Identity;
using AiNexus.Features.Operations;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>The current processing job of a document the user may read; only its owner who may still edit the document controls it.</summary>
internal static class GetDocumentJob
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/job", async (Guid id, ICurrentUser user, NexusDbContext db, DocumentAccess documents, CancellationToken ct) =>
            (await HandleAsync(db, documents, user.Id, id, ct)).ToHttpResult())
        .WithName("DocumentJob").Produces<DocumentJobDto>();

    private static async Task<Result<DocumentJobDto>> HandleAsync(NexusDbContext db, DocumentAccess documents, Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        var job = await db.Set<BackgroundJob>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == document.Value.JobId, ct);
        if (job is null) return KnowledgeErrors.DocumentNotFound;
        var info = await documents.DescribeAsync(actor, document.Value, ct);
        return new DocumentJobDto(JobService.Describe(job), job.OwnerId == actor && info.CanEdit);
    }
}
