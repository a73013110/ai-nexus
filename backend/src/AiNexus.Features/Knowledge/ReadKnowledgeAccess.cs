using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Knowledge;

/// <summary>The owner reads who else may view or edit a collection.</summary>
internal static class ReadKnowledgeAccess
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/collections/{id:guid}/access", async (Guid id, ICurrentUser user, ResourceAccess access, CancellationToken ct) =>
            Results.Ok(await access.AclAsync(user.Id, id, KnowledgeCollection.Kind, ct)))
        .WithName("GetKnowledgeAccess").Produces<ResourceAclDto>();
}
