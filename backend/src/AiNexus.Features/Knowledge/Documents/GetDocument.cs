using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>A document the user may read, with whether they may edit it and the state of its current job.</summary>
internal sealed class GetDocument(DocumentAccess documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", (Guid id, ICurrentUser user, GetDocument handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetDocument");

    public Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => documents.DetailAsync(actor, id, ct);
}
