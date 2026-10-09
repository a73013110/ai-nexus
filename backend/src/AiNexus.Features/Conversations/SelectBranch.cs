using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Conversations;

// Every check needs the database (the leaf must be a message of this conversation), so this request has no validator.
public sealed record SelectBranchRequest(Guid LeafId);

/// <summary>Shows another version (leaf message) of one of the user's own idle conversations. Runs under the conversation's generation lock.</summary>
internal sealed class SelectBranch(NexusDbContext db, GenerationScheduler scheduler, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPatch("/{id:guid}/branch", async (Guid id, SelectBranchRequest body, ICurrentUser user, SelectBranch handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body.LeafId, ct)).ToHttpResult())
        .WithName("SelectBranch");

    public async Task<Result> HandleAsync(Guid owner, Guid id, Guid leaf, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            var conversation = await db.OwnedConversationAsync(owner, id, ct);
            if (conversation is null) return ConversationsErrors.NotFound;
            if (await db.HasActiveRunAsync(id, ct)) return ConversationsErrors.GenerationActive;
            if (!await db.Set<Message>().AnyAsync(x => x.Id == leaf && x.ConversationId == id, ct)) return ConversationsErrors.MessageNotFound;
            conversation.ActiveLeafId = leaf;
            conversation.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Result.Success;
        }
        finally { conversationLock.Dispose(); }
    }
}
