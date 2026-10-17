using AiNexus.Features.Collaboration;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Projects.Contents;

/// <summary>使用者可讀取的專案中，使用者自己最新的 100 個對話；對話只對擁有者可見。</summary>
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
