using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge;

/// <summary>A document the user may read, with whether they may edit it and the state of its current job.</summary>
internal static class GetDocument
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}", async (Guid id, ICurrentUser user, DocumentAccess documents, CancellationToken ct) =>
            (await documents.DetailAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("GetDocument").Produces<DocumentDto>();
}
