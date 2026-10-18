using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge.Documents;

/// <summary>使用者可讀取的文件，附帶其是否可編輯與目前工作的狀態。</summary>
internal sealed class GetDocument(DocumentAccess documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetDocument handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetDocument");

    public Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => documents.DetailAsync(actor, id, ct);
}
