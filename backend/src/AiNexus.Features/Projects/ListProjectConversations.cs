using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Identity.Users;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Projects;

/// <summary>The user's own newest 100 conversations in a project they may read; conversations stay private to their owner.</summary>
internal sealed class ListProjectConversations(NexusDbContext db, ResourceAccess access)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/conversations", (Guid id, ICurrentUser user, ListProjectConversations handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync());

    public async Task<Result<IReadOnlyList<ConversationDto>>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (await access.RequireAsync(actor, id, Project.Kind, ct) is { IsSuccess: false } denied) return denied.Error;
        return (await db.Conversations.Include(x => x.Labels).AsNoTracking().Where(x => x.OwnerId == actor && x.ProjectId == id).OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync(ct)).Select(x => x.ToDto()).ToList();
    }
}
