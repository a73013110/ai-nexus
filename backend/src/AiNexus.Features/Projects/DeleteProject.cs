using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Projects;

/// <summary>Owner-only soft delete through the shared resource lifecycle; conversations, files and artifacts are detached, not destroyed.</summary>
internal static class DeleteProject
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, ResourceLifecycle lifecycle, CancellationToken ct) =>
        {
            await lifecycle.DeleteAsync(user.Id, id, Project.Kind, ct);
            return Results.NoContent();
        });
}
