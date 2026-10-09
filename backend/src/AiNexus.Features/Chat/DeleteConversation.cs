using AiNexus.Features.Attachments;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Events;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Conversations;

namespace AiNexus.Features.Chat;

/// <summary>Raised in the deleting transaction; subscribers (Sharing revokes the conversation's shares) run before it is saved.</summary>
public sealed record ConversationDeleted(Guid ConversationId, Guid OwnerId) : IDomainEvent;

/// <summary>
/// Soft-deletes one of the user's own idle conversations: under the conversation's generation lock and the attachment write lock,
/// in a transaction holding the owner's quota lock, it unlinks the message attachments, raises <see cref="ConversationDeleted"/>
/// (its shares are revoked before the save) and marks files no longer used. Files pending deletion are removed after the
/// locks are released.
/// </summary>
internal sealed class DeleteConversation(NexusDbContext db, GenerationScheduler scheduler, AttachmentWriteLock attachmentWrites, DomainEvents events, AttachmentLifecycle lifecycle, AttachmentQuota quota)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, DeleteConversation handler, CancellationToken ct) => (await handler.HandleAsync(user.Id, id, ct)).ToHttpResult())
        .WithName("DeleteConversation").Produces(204);

    public async Task<Result> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var conversationLock = await scheduler.LockConversationAsync(id, ct);
        try
        {
            await attachmentWrites.Gate.WaitAsync(ct);
            try
            {
                var conversation = await db.OwnedConversationAsync(owner, id, ct);
                if (conversation is null) return ConversationErrors.NotFound;
                if (await db.HasActiveRunAsync(id, ct)) return ConversationErrors.GenerationActive;
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await quota.LockOwnerAsync(owner, ct);
                var links = await db.Set<MessageAttachment>().Where(x => db.Set<Message>().Any(m => m.Id == x.MessageId && m.ConversationId == id)).ToListAsync(ct);
                var fileIds = links.Select(x => x.AttachmentId).Distinct().ToList();
                db.Set<MessageAttachment>().RemoveRange(links);
                conversation.IsDeleted = true;
                events.Raise(new ConversationDeleted(id, owner));
                db.AuditEvents.Add(new() { OwnerId = owner, Action = "conversation.deleted", ResourceId = id });
                await db.SaveChangesAsync(ct);
                await lifecycle.MarkUnusedAsync(fileIds, ct);
                await transaction.CommitAsync(ct);
            }
            finally { attachmentWrites.Gate.Release(); }
        }
        finally { conversationLock.Dispose(); }
        await lifecycle.DeletePendingAsync(ct);
        return Result.Success;
    }
}
