using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>The owner reads who else may view or edit a collection.</summary>
internal sealed class ReadKnowledgeAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections/{id:guid}/access", (Guid id, ICurrentUser user, ReadKnowledgeAccess handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("GetKnowledgeAccess");

    public Task<Result<ResourceAclDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct) => access.AclAsync(actor, id, KnowledgeCollection.Kind, ct);
}
