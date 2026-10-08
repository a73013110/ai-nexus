using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Knowledge;

/// <summary>The owner replaces a collection's named members and group grants.</summary>
internal static class SaveKnowledgeAccess
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/collections/{id:guid}/access", async (Guid id, ResourceAclRequest request, ICurrentUser user, ResourceAccess access, CancellationToken ct) =>
        {
            await access.SetAclAsync(user.Id, id, KnowledgeCollection.Kind, request, ct);
            return Results.NoContent();
        })
        .WithName("SaveKnowledgeAccess").Produces(204);
}
