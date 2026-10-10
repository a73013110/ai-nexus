using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Collections;

/// <summary>The owner replaces a collection's named members and group grants.</summary>
internal sealed class SaveKnowledgeAccess(ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/collections/{id:guid}/access", (Guid id, ResourceAclRequest request, ICurrentUser user, SaveKnowledgeAccess handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, request, ct).ToHttpResultAsync())
        .WithName("SaveKnowledgeAccess");

    public Task<Result> HandleAsync(Guid actor, Guid id, ResourceAclRequest request, CancellationToken ct) => access.SetAclAsync(actor, id, KnowledgeCollection.Kind, request, ct);
}
