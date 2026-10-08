using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>The extracted text of every page of a document the user may read, with whether AI recognition needs review.</summary>
internal static class ListDocumentPages
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/pages", async (Guid id, ICurrentUser user, NexusDbContext db, DocumentAccess documents, CancellationToken ct) =>
            (await HandleAsync(db, documents, user.Id, id, ct)).ToHttpResult())
        .WithName("DocumentPages").Produces<IReadOnlyList<DocumentPageDto>>();

    private static async Task<Result<IReadOnlyList<DocumentPageDto>>> HandleAsync(NexusDbContext db, DocumentAccess documents, Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        return Result<IReadOnlyList<DocumentPageDto>>.Ok(await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.PageNumber)
            .Select(x => new DocumentPageDto(x.PageNumber, x.Text, x.Extraction, x.NeedsReview)).ToListAsync(ct));
    }
}
