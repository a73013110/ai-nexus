using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects;

/// <summary>The user's own newest 100 conversations in a project they may read; conversations stay private to their owner.</summary>
internal static class ListProjectConversations
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/conversations", async (Guid id, ICurrentUser user, NexusDbContext db, ResourceAccess access, CancellationToken ct) =>
            Results.Ok(await HandleAsync(db, access, user.Id, id, ct)))
        .Produces<IReadOnlyList<ConversationDto>>();

    private static async Task<IReadOnlyList<ConversationDto>> HandleAsync(NexusDbContext db, ResourceAccess access, Guid actor, Guid id, CancellationToken ct)
    {
        await access.RequireAsync(actor, id, Project.Kind, ct);
        return (await db.Conversations.Include(x => x.Labels).AsNoTracking().Where(x => x.OwnerId == actor && x.ProjectId == id && !x.IsDeleted).OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct)).Select(x => x.ToDto()).ToList();
    }
}
