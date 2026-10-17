using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>使用者可讀取文件的每一頁擷取文字，並標示 AI 辨識結果是否需要人工審閱。</summary>
internal sealed class ListDocumentPages(NexusDbContext db, DocumentAccess documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/pages", (Guid id, ICurrentUser user, ListDocumentPages handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DocumentPages");

    public async Task<Result<IReadOnlyList<DocumentPageDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        return Result<IReadOnlyList<DocumentPageDto>>.Ok(await db.Set<DocumentPage>().AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.PageNumber)
            .Select(x => new DocumentPageDto(x.PageNumber, x.Text, x.Extraction, x.NeedsReview)).ToListAsync(ct));
    }
}
